# Release Checklist

- Confirm Git is clean and review the release diff.
- Run backend restore, build, tests, NuGet vulnerability audit, and EF pending-model check.
- Run Flutter dependency resolution, analysis, tests, dependency review, and Windows release build.
- Publish the API in Release configuration without source-tree publish artifacts.
- Scan tracked/staged files for secrets, private keys, database archives, and generated binaries.
- Take and validate a current backup, restore it into a generated isolated database using the external maintenance identity, compare migration/schema/data sanity, remove the validation database, and record the recovery plan.
- Apply migrations explicitly with the deployment identity.
- Start the release API with external configuration and verify live/ready health, login, authorization, and a database-backed read.
- Smoke-test the packaged Windows client and real peripherals.
- Tag the accepted release candidate only after deployment-environment acceptance.
