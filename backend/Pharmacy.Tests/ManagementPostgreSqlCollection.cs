namespace Pharmacy.Tests;

// Report seeding uses long rollback transactions against the shared integration database.
// Isolate this fixture from other classes' serializable posting transactions; concurrency
// tests still exercise concurrent production calls inside their own test methods.
[CollectionDefinition("Management PostgreSQL reporting", DisableParallelization = true)]
public sealed class ManagementPostgreSqlCollection;
