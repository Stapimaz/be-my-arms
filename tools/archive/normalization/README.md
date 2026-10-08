# One-time pre-Phase-3 migration — do not rerun

Historical executor/generator snapshots from the 2026-10-08 normalization. They assume the
old numbered layout, original working locations and ignored `Builds/Normalization` staging.
They are **not supported maintenance commands** and cannot be rerun from this archive as-is.

The exact ordered plan and compact verification record are retained in
`docs/archive/normalization`. The migration used AssetDatabase moves, preserved every original
GUID, renamed assemblies/types without changing their dependency boundaries, resolved two type
name collisions, updated serialized names and repaired pre-existing environment metadata.

For a future change, work from current domain paths and drive the live Editor. The maintained
read-only audit is `tools/maintenance/audit-unity-references.cs`; run/build/QA entry points are
documented in `docs/DEV_ENVIRONMENT.md`.
