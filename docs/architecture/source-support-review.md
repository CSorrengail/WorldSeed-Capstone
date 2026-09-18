# Source-Support Review

Every non-question rule draft now carries one or more source-support entries. Each entry contains the original source-note ID and an exact excerpt quoted from that note. WorldSeed normalizes whitespace and verifies that the excerpt occurs in the stored note before it accepts the draft.

This is evidence for review, not a claim of perfect semantic proof. It prevents a model from attaching an arbitrary quotation or a nonexistent source note to a rule. The future review UI should present the proposed rule beside these excerpts and the full original note, so the designer can decide whether the proposed wording is a faithful interpretation.

    original source note
           |
    exact quoted excerpt <- model proposal
           |
    structural + literal-source validation
           |
    reviewable rule draft (never canonical automatically)
