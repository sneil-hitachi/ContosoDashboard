# Document Upload Test Files

These small synthetic fixtures are for the local document-upload workflow. They contain no real company or personal information.

- `project-update.txt`: Expected to pass upload when the configured scanner reports clean. Suggested category: Project Documents.
- `team-reference.txt`: Expected to pass upload when the configured scanner reports clean. Suggested category: Team Resources.
- `personal-notes.txt`: Expected to pass upload when the configured scanner reports clean. Suggested category: Personal Files. Leave the project blank.
- No EICAR test file is included. Host endpoint protection quarantined both the direct test payload and its encoded source during fixture creation.

The normal samples use the supported `.txt` extension and are intentionally small. Scanner tests require local ClamAV and signatures; if either is unavailable, uploads should fail closed as documented in the feature quickstart. Do not disable host protection to run an EICAR test; use a dedicated scanner-test environment and follow its approved procedure.
