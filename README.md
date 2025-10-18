# Enterprise Software Development Skills

Production-validated development patterns and specialized knowledge for enterprise software development.

> **DISCLAIMER**: This is an experimental first attempt at building Claude Code skills. While the patterns are based on production codebases, these skills are provided as-is without warranty. Community input and contributions are welcome! Use at your own risk and always review generated code before deploying to production environments.

## What are Skills?

Skills are folders of instructions, scripts, and resources that Claude loads dynamically to improve performance on specialized tasks. These skills teach Claude how to complete specific development tasks in a repeatable way, following production-validated patterns and best practices from real enterprise codebases.

For more information about skills in Claude Code, check out:
- [What are skills?](https://support.claude.com/en/articles/12512176-what-are-skills)
- [Using skills in Claude](https://support.claude.com/en/articles/12512180-using-skills-in-claude)
- [Creating custom skills](https://support.claude.com/en/articles/12512198-creating-custom-skills)

## Installation

First, add this marketplace to Claude Code:
```bash
/plugin marketplace add mbundgaard/Skills
```

Then install the skills plugin:
```bash
/plugin install skills@enterprise-software-development
```

## Usage

Once installed, you can use any skill by simply mentioning it in your conversation with Claude Code. For example:

```
"Use the Simphony Extension skill to help me implement a new POS integration"
```

Claude will automatically load the skill and apply the relevant patterns and best practices to your task.

## Available Skills

### [Simphony Extension Development](./simphony-extension-skill)
Comprehensive patterns for Oracle Simphony Extension Application development covering POS Integration, Enterprise, and C#/.NET.

**What you get:**
- Copy-paste ready templates and code examples
- Domain-specific implementation guides
- Scale-appropriate pattern selection
- Anti-pattern identification and best practices

**Categories:** POS Integration, Enterprise, C#/.NET

---

## Quality Standards

All skills are based on analysis of production codebases with statistical validation across multiple enterprise projects.

## License

MIT License - Commercial use permitted