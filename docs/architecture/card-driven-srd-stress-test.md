# Carta and Wretched & Alone Stress Tests

These compact schemas test non-tactical, solo journaling games. They model only structural patterns, not full SRD text or complete playable games.

Carta tests card records, a physical grid coordinate, a session marker, card-to-prompt relationships, and a partially structured exploration procedure.

Wretched & Alone tests a card oracle, four suit-based failure countdowns, ten-token salvation progress, optional physical-tower resolution, ordered journaling, and procedures whose decisive parts intentionally remain non-deterministic. The tower field is optional because the SRD specifically encourages accessible alternatives.

The tests confirm that cards, towers, tokens, journaling, and solo procedures can be game-defined structures rather than universal meta-schema concepts. No v0.5 change is required by these patterns. Game-data validation now supports enumeration entries, maps, unions, relationship attributes, and collection bounds. The Wretched & Alone example therefore requires four countdown records; the remaining game-specific invariant—that those records cover the four different suits—is deliberately prose-first until the project has evidence for a general keyed-collection constraint.

## Sources

- Carta SRD: https://blind-snake-studio.itch.io/carta-srd
- Wretched & Alone SRD: https://sealedlibrary.itch.io/wretched-alone-srd

The Wretched & Alone source page states that its SRD is available under CC BY 3.0. These examples are short structural models, not reproductions of either SRD.
