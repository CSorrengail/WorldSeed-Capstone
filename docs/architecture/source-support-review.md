# Source-Support Review

Every non-question rule draft now carries one or more source-support entries. The model selects stable passage IDs from the supplied source material rather than attempting to reproduce a quotation. WorldSeed then materializes the exact original passage text from those IDs before it accepts the draft.

This is evidence for review, not a claim of perfect semantic proof. It prevents a model from attaching an arbitrary quotation or a nonexistent source note to a rule. The future review UI should present the proposed rule beside these excerpts and the full original note, so the designer can decide whether the proposed wording is a faithful interpretation.

    original source note
           |
    exact quoted excerpt <- model proposal
           |
    structural + literal-source validation
           |
    reviewable rule draft (never canonical automatically)
