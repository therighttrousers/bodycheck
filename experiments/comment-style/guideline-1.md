# Writing comments and docs

Write comments and docs for a programmer who knows C# and Godot well but has never seen this code. They should be able to read each sentence once and understand it.

## Vocabulary

- Use the standard technical term when one exists: "shortest path", not "best way"; "request", not "ask"; "contains", "suppresses" or "constrains", not "holds". A reader should be able to search for the term.
- Prefer a precise verb to a general one. "Hold", "keep", "go", "ask", "say", "take" and "live" each have many senses; use them only in their most literal sense.

## Sentences

- Name what a sentence is about. Start a summary with a noun phrase ("The dialogue's current state"), not "What ...".
- Pronouns refer back to something already named, never forward.
- Don't leave a verb implied when it would change form or meaning: write "while no dialogue is presented", not "while none is".
- One idea per sentence. If a sentence needs more than one colon, semicolon or "or else", split it.

## Content

- Write few comments. Straightforward code doesn't need one.
- Comment what the code can't say: surprising behaviour, hidden dependencies and invariants, ordering that matters, aliasing, fragility, and the minimum context needed to understand a dense passage.
- Don't restate the logic. Explain why, or what must stay true.
- Write doc comments for a caller who can't see the implementation: preconditions, postconditions, effects on the rest of the system, purpose and when to use it, but only where the signature doesn't already make them clear.
