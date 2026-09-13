# AGENTS.md

## Project: AI-Assisted Tabletop Role-Playing Game Design System

### Project Purpose

This is a 10-week Master's of Artificial Intelligence capstone project for CSC545 - AI Tools at Neumont University.

The project is an AI-assisted, open-ended tabletop role-playing game (TTRPG) design system. The long-term goal is to allow a TTRPG designer to provide relatively unstructured natural-language rules, mechanics, concepts, and design goals. The system interprets that information, establishes a structured representation of the particular game, assists with generating additional game content, validates that content against the game's own rules, evaluates balance where possible, and keeps the human designer in control through approval/rejection/revision.

The system must NOT assume that every TTRPG contains monsters, items, abilities, characters, spells, classes, levels, hit points, armor classes, or other concepts associated with a particular existing game. Those are examples of possible game concepts, not universal concepts.

The central architectural principle is:

> The system has a universal meta-schema that defines how a TTRPG can define its own structures. The meta-schema does not define what a TTRPG must contain.

### Current Architectural Model

The intended conceptual hierarchy is:

    META-SCHEMA
        |
        | Defines how structures can be described
        v
    GAME SCHEMA
        |
        | Defines the structures used by one particular TTRPG
        v
    GAME DATA
        |
        | Contains actual instances/content
        v
    PLAYABLE GAME CONTENT

The three layers must remain conceptually distinct.

The meta-schema is universal across all TTRPGs created with this system.

The game schema is specific to one TTRPG and can define arbitrary entity types, properties, relationships, behaviors, and rules.

Game data consists of actual instances of those game-specific definitions.

### Reference-Table Inspiration

The designer has professional experience with FAST Enterprises and is intentionally taking inspiration from its reference-table pattern.

In the project, reference definitions should conceptually function like metadata/reference tables that establish the structure of other dynamically defined structures.

The meta-schema therefore behaves somewhat like a relational database's reference-table layer:

- It defines what kinds of definitions are possible.
- It defines how those definitions relate to one another.
- It does not contain actual game content.
- A game-specific definition can effectively establish the structure of a virtual "table" or document.
- Actual content can then conform to that game-specific definition.

Do not prematurely hard-code domain concepts such as Monster, Item, Ability, Character, Weapon, Spell, etc.

### Meta-Schema v0.1

The first draft of the meta-schema currently contains these conceptual definition types:

1. EntityType
2. PropertyDefinition
3. DataTypeDefinition
4. RelationshipDefinition
5. RuleDefinition
6. BehaviorDefinition
7. ValidationDefinition
8. GameSchema

The current draft JSON is:

```json
{
  "metaSchema": {
    "name": "TTRPG Design System Meta-Schema",
    "version": "0.1.0",
    "description": "Universal schema used to define the structure of an individual TTRPG.",

    "definitionTypes": {
      "entityType": {
        "description": "Defines a type of game entity that can exist within a TTRPG.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "description": {
            "type": "string",
            "required": false
          },
          "properties": {
            "type": "reference",
            "target": "propertyDefinition",
            "collection": true,
            "required": false
          },
          "relationships": {
            "type": "reference",
            "target": "relationshipDefinition",
            "collection": true,
            "required": false
          },
          "rules": {
            "type": "reference",
            "target": "ruleDefinition",
            "collection": true,
            "required": false
          },
          "behaviors": {
            "type": "reference",
            "target": "behaviorDefinition",
            "collection": true,
            "required": false
          }
        }
      },

      "propertyDefinition": {
        "description": "Defines a property that can exist on an entity type.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "description": {
            "type": "string",
            "required": false
          },
          "dataType": {
            "type": "reference",
            "target": "dataTypeDefinition",
            "required": true
          },
          "required": {
            "type": "boolean",
            "required": true
          },
          "collection": {
            "type": "boolean",
            "required": true
          },
          "defaultValue": {
            "type": "any",
            "required": false
          },
          "rules": {
            "type": "reference",
            "target": "ruleDefinition",
            "collection": true,
            "required": false
          }
        }
      },

      "dataTypeDefinition": {
        "description": "Defines the type of value that a property may contain.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "baseType": {
            "type": "enum",
            "values": [
              "string",
              "integer",
              "decimal",
              "boolean",
              "date",
              "reference",
              "object",
              "any"
            ],
            "required": true
          },
          "referenceTarget": {
            "type": "reference",
            "target": "entityType",
            "required": false
          }
        }
      },

      "relationshipDefinition": {
        "description": "Defines a relationship between two entity types.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "description": {
            "type": "string",
            "required": false
          },
          "source": {
            "type": "reference",
            "target": "entityType",
            "required": true
          },
          "target": {
            "type": "reference",
            "target": "entityType",
            "required": true
          },
          "cardinality": {
            "type": "enum",
            "values": [
              "oneToOne",
              "oneToMany",
              "manyToOne",
              "manyToMany"
            ],
            "required": true
          },
          "bidirectional": {
            "type": "boolean",
            "required": true
          }
        }
      },

      "ruleDefinition": {
        "description": "Defines a game rule. A rule may be fully structured, partially structured, or primarily natural language.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "description": {
            "type": "string",
            "required": true
          },
          "structured": {
            "type": "boolean",
            "required": true
          },
          "conditions": {
            "type": "object",
            "required": false
          },
          "effects": {
            "type": "object",
            "required": false
          },
          "naturalLanguageRule": {
            "type": "string",
            "required": false
          },
          "appliesTo": {
            "type": "reference",
            "target": "entityType",
            "collection": true,
            "required": false
          }
        }
      },

      "behaviorDefinition": {
        "description": "Defines an action, ability, procedure, or other behavior an entity can perform or possess.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "description": {
            "type": "string",
            "required": true
          },
          "activation": {
            "type": "object",
            "required": false
          },
          "conditions": {
            "type": "object",
            "required": false
          },
          "effects": {
            "type": "object",
            "required": false
          },
          "rules": {
            "type": "reference",
            "target": "ruleDefinition",
            "collection": true,
            "required": false
          }
        }
      }
    },

    "validation": {
      "validationDefinition": {
        "description": "Defines a constraint that can be applied to a game structure or game data.",
        "fields": {
          "id": {
            "type": "string",
            "required": true
          },
          "name": {
            "type": "string",
            "required": true
          },
          "description": {
            "type": "string",
            "required": false
          },
          "target": {
            "type": "string",
            "required": true
          },
          "expression": {
            "type": "object",
            "required": false
          },
          "naturalLanguageRule": {
            "type": "string",
            "required": false
          },
          "severity": {
            "type": "enum",
            "values": [
              "information",
              "warning",
              "error"
            ],
            "required": true
          }
        }
      }
    },

    "gameSchema": {
      "description": "A TTRPG-specific schema constructed using the meta-schema.",
      "fields": {
        "id": {
          "type": "string",
          "required": true
        },
        "name": {
          "type": "string",
          "required": true
        },
        "version": {
          "type": "string",
          "required": true
        },
        "description": {
          "type": "string",
          "required": false
        },
        "entityTypes": {
          "type": "reference",
          "target": "entityType",
          "collection": true,
          "required": true
        },
        "dataTypes": {
          "type": "reference",
          "target": "dataTypeDefinition",
          "collection": true,
          "required": false
        },
        "relationships": {
          "type": "reference",
          "target": "relationshipDefinition",
          "collection": true,
          "required": false
        },
        "rules": {
          "type": "reference",
          "target": "ruleDefinition",
          "collection": true,
          "required": false
        },
        "behaviors": {
          "type": "reference",
          "target": "behaviorDefinition",
          "collection": true,
          "required": false
        },
        "validations": {
          "type": "reference",
          "target": "validationDefinition",
          "collection": true,
          "required": false
        }
      }
    }
  }
}
```

### Special Rules and Unstructured Mechanics

The system must not require every game rule to fit a predefined mechanical structure.

A TTRPG may contain specialized mechanics that have no obvious universal representation. For example, a D&D-like creature might have a specialized trait whose behavior is difficult to decompose into generic fields.

Rules therefore have three conceptual levels of representation:

1. Fully structured rules.
2. Partially structured rules supplemented by natural language.
3. Primarily or entirely natural-language rules.

The natural-language rule should remain authoritative. Structured interpretation is derived information that allows the software to validate, reason about, or simulate the rule when possible.

Failure to fully structure a rule should not prevent the rule from existing in the game.

This principle is important:

> The meta-schema defines what information the system can represent, not what information a TTRPG is allowed to contain.

### AI Rule Interpretation

The system is expected to accept unstructured natural-language game rules.

An intended workflow is:

    Designer's unstructured rules
        ↓
    LLM interpretation
        ↓
    Structured game definitions
        ↓
    Human review/approval
        ↓
    Canonical game schema

The LLM should not be assumed to understand every rule perfectly. The system should preserve original natural-language information and distinguish source information from AI-generated interpretations.

The eventual system should be able to identify uncertainty or ambiguous interpretations rather than silently inventing authoritative mechanics.

### Validation Philosophy

Universal TTRPG-specific validation rules should NOT be hard-coded.

Universal structural validation is appropriate, such as:

- malformed data
- invalid references
- missing required fields
- incorrect data types
- invalid schema relationships

Game-specific validation should be derived from the particular TTRPG's definitions and rules.

A game that defines "characters may equip no more than three abilities" can have that constraint represented in its game-specific rules.

A game with no abilities does not need that constraint.

Natural-language rules that cannot be converted into deterministic validation logic can still exist and remain authoritative, although they may not be machine-verifiable.

### Current Technology Direction

Primary development language: C# / .NET.

Expected technologies include:

- C# / .NET
- ASP.NET Core or an equivalent .NET application architecture
- JSON / JSON Schema
- An LLM API
- MongoDB or another document database for persistent game data
- Vector/embedding retrieval if it proves useful
- Docker / Docker Compose
- Git / GitHub
- xUnit or another .NET testing framework

The database choice is not final.

The meta-schema should be designed independently of the persistence technology.

The developer has relational database experience and is comfortable thinking in terms of reference tables, relationships, keys, and metadata. That experience should be used when designing the conceptual model even if the eventual persistence layer is a document database.

### Containerization Requirement

The project should be containerized using Docker.

The goal is for the complete application environment to be portable and easily testable on another machine.

The intended containerization includes:

- application/backend
- database
- supporting services
- other project infrastructure

The AI models themselves are excluded from the containerization requirement. They may be accessed externally.

Prefer Docker Compose for local orchestration unless there is a compelling reason to use another approach.

Do not introduce container complexity prematurely, but the architecture should remain compatible with this requirement.

### Development Priorities

Current priority is NOT UI, advanced balance systems, image generation, or a production-grade deployment.

The immediate architectural priority is understanding and testing the meta-schema.

Before implementing substantial application code:

1. Examine the current meta-schema.
2. Identify concepts that are genuinely universal.
3. Identify concepts that are accidentally tied to common TTRPGs such as D&D.
4. Stress-test the model against radically different hypothetical TTRPGs.
5. Revise the meta-schema as necessary.
6. Only then begin implementing persistent models and application services.

Do not treat the current v0.1 JSON as immutable. It is a starting hypothesis.

### Testing the Meta-Schema

Use deliberately different hypothetical games to test whether the meta-schema remains generic.

Useful examples include:

- A D&D-like fantasy RPG with creatures, weapons, spells, abilities, and characters.
- A narrative RPG with almost no numeric statistics.
- A science-fiction game where ships are the primary entities.
- A game where the primary entities are factions, resources, and political relationships.
- A strange/custom game containing mechanics that do not fit conventional RPG categories.

The question is not whether these games can be represented with the same game schema.

The question is whether they can all construct their different schemas using the same meta-schema.

### Important Architectural Principle

The system is not intended to be "an LLM that generates TTRPG content."

The intended distinction is:

    LLM = interprets and proposes
    Structured knowledge = stores canonical game information
    Software = enforces structural constraints and performs deterministic operations
    Human designer = approves and controls authoritative game decisions

The project should preserve this distinction throughout development.

### Future Architecture

The eventual architecture is expected to develop toward:

    Natural-language rules
            ↓
    Rule interpretation
            ↓
    Game-specific schema
            ↓
    Structured knowledge base
            ↓
    Content generation/reasoning
            ↓
    Validation
            ↓
    Balance evaluation where applicable
            ↓
    Revision
            ↓
    Human approval
            ↓
    Canonical game knowledge

Do not implement the entire pipeline immediately. Build and validate the underlying representation first.

### Coding Guidance

Prefer clear, maintainable C# over premature abstraction.

Do not hard-code TTRPG concepts merely because they are common.

Do not create classes such as Monster, Spell, Character, or Item as universal domain classes unless later architectural work demonstrates that they belong in a genuinely game-agnostic abstraction.

Prefer generic structures that correspond to the meta-schema.

Keep source natural-language rules separate from derived/structured interpretations.

Preserve versioning because both the meta-schema and individual game schemas will likely evolve.

Write tests for the meta-schema and its interpretation before building large amounts of generation functionality.

When a design decision is uncertain, document the uncertainty rather than silently locking in an assumption.

### Current Immediate Task

The next major task is to review and refine the meta-schema v0.1.

Do not immediately create a full application.

First determine whether the proposed meta-schema can represent arbitrary TTRPG structures without requiring changes to the universal layer.

The developer is specifically interested in whether concepts such as:

- entity types
- properties
- data types
- relationships
- rules
- behaviors
- validations

are sufficient, excessive, or incorrectly defined.

The developer expects the meta-schema to change as testing reveals weaknesses.
