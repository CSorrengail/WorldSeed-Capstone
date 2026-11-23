using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WorldSeed.LlmIntegration;

namespace WorldSeed.RuleDrafting;

public sealed record RuleDraftProgress(int CompletedPassages, int TotalPassages);

public sealed partial class RuleDraftingService
{
    public const int MaximumIntakePassages = 6;
    public const int MaximumIntakeCharacters = 2400;
    public const int MaximumIntakeOutputTokens = 3200;
    private sealed record IntakePart(SourcePassage Original, SourcePassage Fragment);

    private async Task<RuleDraftingResult> AdvanceSourceIntakeAsync(RuleDraftConversation conversation,
        IReadOnlyDictionary<string, string> sources, IReadOnlyList<SourcePassage> passages,
        CancellationToken cancellationToken, IProgress<RuleDraftProgress>? progress)
    {
        var primaryId = conversation.SourceMaterialIds[0];
        var originals = passages.Where(passage => passage.SourceNoteId == primaryId).ToArray();
        if (originals.Length == 0) throw new RuleDraftFormatException("No source passages are available for this note.");
        var answers = passages.Where(passage => passage.SourceNoteId != primaryId).ToArray();
        if (answers.Sum(passage => passage.Text.Length) > 8000)
            throw new RuleDraftFormatException("The accumulated designer answers exceed this intake context. Use shorter answers before continuing.");
        // Fragments are transport-only. Saved evidence is restored to the complete,
        // original catalog passage, so later validation never sees invented anchors.
        var parts = originals.SelectMany(original => Enumerable.Range(0, (original.Text.Length + 1199) / 1200)
            .Select(index => new IntakePart(original, new SourcePassage(original.Id + $":segment{index + 1}", original.SourceNoteId,
                original.Text.Substring(index * 1200, Math.Min(1200, original.Text.Length - index * 1200)))))).ToArray();
        var batches = new List<IntakePart[]>(); var pending = new List<IntakePart>(); var characters = 0;
        foreach (var part in parts)
        {
            if (pending.Count > 0 && (pending.Count >= MaximumIntakePassages || characters + part.Fragment.Text.Length > MaximumIntakeCharacters))
            { batches.Add(pending.ToArray()); pending.Clear(); characters = 0; }
            pending.Add(part); characters += part.Fragment.Text.Length;
        }
        if (pending.Count > 0) batches.Add(pending.ToArray());
        var drafts = new List<StructuredRuleDraft>(); var questions = new List<string>(); var completed = 0; var calls = 0;
        var sourceAliases = sources.Keys.Select((id, index) => (Id: id, Alias: "n" + (index + 1)))
            .ToDictionary(item => item.Alias, item => item.Id, StringComparer.Ordinal);
        foreach (var batch in batches) await ProcessAsync(batch);

        var useful = drafts.FirstOrDefault(draft => draft.Rules.Count > 0);
        StructuredRuleDraft? combined = useful is null ? null : new StructuredRuleDraft(useful.Title, useful.Intent,
            drafts.SelectMany((draft, index) => draft.Rules.Select(rule => rule with { Id = $"part{index + 1:D3}-{rule.Id}" })).ToArray(),
            drafts.SelectMany((draft, index) => draft.Concepts.Select(concept => concept with { Id = $"part{index + 1:D3}-{concept.Id}" })).ToArray(),
            drafts.SelectMany(draft => draft.Assumptions).Distinct().ToArray(),
            drafts.SelectMany(draft => draft.OpenQuestions).Distinct().ToArray(),
            drafts.SelectMany(draft => draft.Exclusions).Distinct().ToArray());
        var uniqueQuestions = questions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var turn = uniqueQuestions.Length > 0
            ? new RuleDraftTurn(RuleDraftAction.AskClarifyingQuestion, uniqueQuestions[0], uniqueQuestions, combined)
            : combined is not null ? new RuleDraftTurn(RuleDraftAction.PresentDraft, null, [], combined)
            : throw new RuleDraftFormatException("No explicit game rules were found in the source. No rule draft was saved.");
        if (combined is not null)
        {
            var issues = new StructuredRuleDraftValidator().Validate(combined, true, sources.Keys.ToHashSet(StringComparer.Ordinal), sources);
            if (issues.Count > 0) throw new RuleDraftFormatException(string.Join(" ", issues));
        }
        // Store a durable, aggregated transcript, not short transport aliases or
        // only the final batch. Individual raw responses remain diagnostic output.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        var transcript = new JsonObject { ["action"] = turn.Action == RuleDraftAction.PresentDraft ? "presentDraft" : "askClarifyingQuestion" };
        if (combined is not null) transcript["draft"] = JsonSerializer.SerializeToNode(combined, options);
        if (uniqueQuestions.Length > 0) transcript["clarifyingQuestions"] = JsonSerializer.SerializeToNode(uniqueQuestions);
        return new RuleDraftingResult(turn, transcript.ToJsonString());

        async Task ProcessAsync(IntakePart[] batch, bool coverageRetry = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new(completed, parts.Length));
            var first = Array.IndexOf(parts, batch[0]);
            var last = Array.IndexOf(parts, batch[^1]);
            var context = parts.Take(1).Concat(parts.Skip(Math.Max(0, first - 2)).Take(Math.Min(first, 2)))
                .Concat(parts.Skip(last + 1).Take(2))
                .Where(part => !batch.Contains(part)).DistinctBy(part => part.Fragment.Id).Select(part => part.Fragment).ToArray();
            var available = batch.Select(part => part.Fragment).Concat(context).Concat(answers).DistinctBy(passage => passage.Id).ToArray();
            var passageAliases = available.Select((passage, index) => (Alias: "p" + (index + 1), Passage: passage))
                .ToDictionary(item => item.Alias, item => item.Passage, StringComparer.Ordinal);
            var reverseSources = sourceAliases.ToDictionary(item => item.Value, item => item.Key, StringComparer.Ordinal);
            var reversePassages = passageAliases.ToDictionary(item => item.Value.Id, item => item.Key, StringComparer.Ordinal);
            var aliasPassages = available.Select(passage => new SourcePassage(reversePassages[passage.Id], reverseSources[passage.SourceNoteId], passage.Text)).ToArray();
            var allowedSources = available.Select(passage => passage.SourceNoteId).ToHashSet(StringComparer.Ordinal);
            var schema = RuleDraftResponseSchema.Create(allowedSources.Select(id => reverseSources[id]).ToHashSet(StringComparer.Ordinal), aliasPassages);
            schema["properties"]!["draft"]!["properties"]!["rules"]!["minItems"] = 0;
            schema["properties"]!["draft"]!["properties"]!["rules"]!["maxItems"] = 8;
            schema["properties"]!["draft"]!["properties"]!["concepts"]!["maxItems"] = 4;
            schema["properties"]!["passageNotes"] = JsonNode.Parse("""{"type":"array","items":{"type":"object","additionalProperties":false,"required":["passageId","disposition","reason"],"properties":{"passageId":{"type":"string"},"disposition":{"type":"string","enum":["metadata","unresolved"]},"reason":{"type":"string","minLength":1,"maxLength":500}}}}""");
            schema["properties"]!["passageNotes"]!["items"]!["properties"]!["passageId"]!["enum"] = new JsonArray(batch.Select(part => (JsonNode?)JsonValue.Create(reversePassages[part.Fragment.Id])).ToArray());
            string Format(IEnumerable<SourcePassage> items) => string.Join("\n", items.Select(item => $"[{reversePassages[item.Id]} | {reverseSources[item.SourceNoteId]}] {item.Text}"));
            var messages = new List<LlmMessage>
            {
                new(LlmMessageRole.System, SystemPrompt),
                new(LlmMessageRole.System, "This is bounded source extraction, not an interview about the task. Extract ALL stated facts in the target passages. Context passages only resolve headings/pronouns; do not draft unrelated context-only rules. Designer answers override uncertainty when explicit; cite their anchors when used. Use ONLY the compact note and passage IDs shown. Every target passage must be cited by a rule or listed in passageNotes as metadata or unresolved with a short reason. Metadata-only batches may return an empty rules array. Do not discard facts to fit the response. Ask only concrete questions about undefined game mechanics, never what to do with this text or which aspect to focus on."),
                new(LlmMessageRole.System, "Target passages:\n" + Format(batch.Select(part => part.Fragment))),
                new(LlmMessageRole.System, "Read-only neighboring context:\n" + Format(context)),
                new(LlmMessageRole.System, "Saved designer answers:\n" + Format(answers))
            };
            if (!string.IsNullOrWhiteSpace(conversation.WorkflowInstruction)) messages.Add(new(LlmMessageRole.System, conversation.WorkflowInstruction));
            if (coverageRetry) messages.Add(new(LlmMessageRole.System, "A prior draft omitted these target passages. Process ONLY these targets, using neighbors solely for context. A heading, navigation, or license notice is metadata, not a new game rule. If no rule can be extracted, return draft.rules: [] and passageNotes: [{\"passageId\":\"p1\",\"disposition\":\"metadata\",\"reason\":\"Explain why\"}] using the actual target IDs. Never substitute rules from neighboring context."));
            foreach (var message in conversation.Messages.Where(message => message.Role == LlmMessageRole.User && !sources.Values.Contains(message.Content, StringComparer.Ordinal)))
                messages.Add(new(LlmMessageRole.User, message.Content));
            messages.Add(new(LlmMessageRole.User, "Extract the target source passages into complete, faithful rules now. Return the specified JSON only."));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (++calls > parts.Length * 4 + 4) throw new RuleDraftFormatException("The model repeatedly failed bounded source extraction. No completed draft was saved.");
                var response = await _client.CompleteAsync(new(messages, Temperature: 0.1, MaxOutputTokens: MaximumIntakeOutputTokens, ResponseSchema: schema), cancellationToken);
                var accepted = false;
                try
                {
                    var root = JsonNode.Parse(response.Content) as JsonObject ?? throw new RuleDraftFormatException("The model did not return a JSON object.");
                    DecodeCitations(root, sourceAliases, passageAliases);
                    var catalog = available.Concat(batch.Select(part => part.Original)).DistinctBy(passage => passage.Id).ToArray();
                    var parsed = _parser.Parse(root.ToJsonString(), allowedSources, sources, catalog, allowEmptyDraft: true);
                    if (parsed.Action == RuleDraftAction.AskClarifyingQuestion)
                    {
                        if (parsed.ClarifyingQuestions.Any(IsTaskQuestion))
                        {
                            if (attempt == 0) { messages.Add(new(LlmMessageRole.System, "The extraction task is already specified. Do not ask what to do with the document or which aspect interests the user. Extract all supplied target rules; only ask about a concrete undefined mechanic.")); continue; }
                            throw new RuleDraftFormatException("The model kept asking about the task instead of extracting source rules. No completed draft was saved.");
                        }
                        questions.AddRange(parsed.ClarifyingQuestions);
                    }
                    else
                    {
                        var draft = parsed.Draft!;
                        if (draft.Rules.Count > 8) throw new RuleDraftFormatException("The bounded draft exceeded its rule budget.");
                        var notes = ReadPassageNotes(root, passageAliases, batch);
                        bool Supports(RuleSourceSupport support, IntakePart part) =>
                            support.PassageId == part.Fragment.Id || support.PassageId == part.Original.Id ||
                            support.PassageId is null && support.SourceNoteId == part.Original.SourceNoteId && part.Fragment.Text.Contains(support.Excerpt, StringComparison.Ordinal);
                        // Do not duplicate context-only rules when repairing an omitted heading.
                        draft = draft with { Rules = draft.Rules.Where(rule => rule.SourceSupport.Any(support => batch.Any(part => Supports(support, part)))).ToArray() };
                        var missing = batch.Where(part => !draft.Rules.SelectMany(rule => rule.SourceSupport).Any(support => Supports(support, part)) && !notes.ContainsKey(part.Fragment.Id)).ToArray();
                        if (coverageRetry)
                            foreach (var part in missing)
                                notes[part.Fragment.Id] = ("unresolved", $"[{part.Original.Id}] Extraction Needs Review: The model did not account for this source passage after a focused retry. Original Text: {part.Fragment.Text}");
                        var restored = draft with
                        {
                            Rules = draft.Rules.Select(rule => rule with { SourceSupport = rule.SourceSupport.Select(support =>
                            {
                                var part = parts.FirstOrDefault(part => part.Fragment.Id == support.PassageId);
                                return part is null ? support : new RuleSourceSupport(part.Original.SourceNoteId, part.Original.Text, part.Original.Id);
                            }).Distinct().ToArray() }).ToArray(),
                            Exclusions = draft.Exclusions.Concat(notes.Values.Where(note => note.Kind == "metadata").Select(note => note.Text)).ToArray(),
                            OpenQuestions = draft.OpenQuestions.Concat(notes.Values.Where(note => note.Kind == "unresolved").Select(note => note.Text)).ToArray()
                        };
                        drafts.Add(restored);
                        accepted = true;
                        if (missing.Length > 0 && !coverageRetry)
                        {
                            completed += batch.Length - missing.Length;
                            await ProcessAsync(missing, coverageRetry: true);
                            return;
                        }
                    }
                    completed += batch.Length; progress?.Report(new(completed, parts.Length)); return;
                }
                catch (JsonException) when (!accepted && batch.Length > 1)
                { await SplitAsync(batch, coverageRetry); return; }
                catch (RuleDraftFormatException ex) when (!accepted && batch.Length > 1 && !ex.Message.Contains("task instead", StringComparison.Ordinal))
                { await SplitAsync(batch, coverageRetry); return; }
                catch (Exception ex) when (!accepted && attempt == 0 && (ex is JsonException or RuleDraftFormatException))
                { messages.Add(new(LlmMessageRole.System, "Return a complete compact replacement for these target passages. Account for each target, use only provided citation IDs, and close the JSON object. Error: " + ex.Message)); }
                catch (JsonException)
                { throw new RuleDraftFormatException("The model returned incomplete or invalid JSON even for a single source passage. No completed draft was saved."); }
            }
            throw new RuleDraftFormatException("The model could not extract this bounded source passage. No completed draft was saved.");
        }
        async Task SplitAsync(IntakePart[] batch, bool coverageRetry)
        {
            var middle = batch.Length / 2;
            await ProcessAsync(batch[..middle], coverageRetry); await ProcessAsync(batch[middle..], coverageRetry);
        }
    }

    private static void DecodeCitations(JsonObject root, IReadOnlyDictionary<string, string> sources, IReadOnlyDictionary<string, SourcePassage> passages)
    {
        foreach (var rule in (root["draft"]?["rules"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            if (rule["sourceNoteIds"] is JsonArray ids)
                for (var index = 0; index < ids.Count; index++)
                    if (ids[index]?.GetValue<string>() is { } id && sources.TryGetValue(id, out var actual)) ids[index] = actual;
            foreach (var support in (rule["sourceSupport"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (support["sourceNoteId"]?.GetValue<string>() is { } id && sources.TryGetValue(id, out var actual)) support["sourceNoteId"] = actual;
                if (support["passageIds"] is JsonArray anchors)
                    for (var index = 0; index < anchors.Count; index++)
                        if (anchors[index]?.GetValue<string>() is { } anchor && passages.TryGetValue(anchor, out var passage)) anchors[index] = passage.Id;
            }
        }
    }

    private static Dictionary<string, (string Kind, string Text)> ReadPassageNotes(JsonObject root, IReadOnlyDictionary<string, SourcePassage> aliases, IReadOnlyList<IntakePart> targets)
    {
        var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var note in (root["passageNotes"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var alias = note["passageId"]?.GetValue<string>() ?? "";
            var kind = note["disposition"]?.GetValue<string>(); var reason = note["reason"]?.GetValue<string>();
            if (!aliases.TryGetValue(alias, out var passage) || !targets.Any(target => target.Fragment.Id == passage.Id) ||
                kind is not ("metadata" or "unresolved") || string.IsNullOrWhiteSpace(reason))
                throw new RuleDraftFormatException("A passage note must identify a target passage and explain metadata or unresolved content.");
            var original = targets.First(target => target.Fragment.Id == passage.Id).Original;
            result[passage.Id] = (kind, $"[{original.Id}] {reason}");
        }
        return result;
    }

    private static bool IsTaskQuestion(string question) => Regex.IsMatch(Regex.Replace(question, @"\s+", " "),
        @"\b(what (?:would|do) you (?:like|want) me to do|how can I (?:help|assist)|which (?:part|aspect|section).*(?:focus|interested)|are you interested in (?:a|any) specific|what (?:would|do) you (?:like|want).*(?:focus on|do with (?:this|the) (?:text|document)))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
