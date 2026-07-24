# Compono Design Principles

## Default Philosophy

Compono is designed for modern .NET.

Source generation is the default execution model. Generated composition plans are the expected execution path.

## Reflection Policy

Runtime reflection is not part of the default architecture.

If supported in the future, it must require an explicit opt-in by the consuming project (for example via a compiler-visible MSBuild property or dedicated compatibility package). Reflection should never silently become the fallback path.

This mirrors the philosophy used by AlexaVoxCraft's interception support: modern behavior is the default; compatibility is intentional.

## Guiding Principles

- Composition over object generation
- Predictability over magic
- Source-generated first
- Deterministic by default
- Diagnostics are a feature
- Performance is a feature
- Modular architecture
- Modern .NET first
