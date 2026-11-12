# API integration test fixture

Create and dispose a separate `CustomWebApplicationFactory` per test class.
Call `CreateClient()` before accessing `factory.Data`: the host initializes
and seeds its unique in-memory database on first use. Send `X-Test-User-Id`
using a seeded ID and `X-Test-Roles` as a comma-separated list. The seeded
users use the same value for their internal ID and Entra subject. These
headers are trusted only in the test factory, never by the production API.

The fixture uses `EnsureCreated`, not migrations: the production migrations
target SQL Server and cannot run on SQLite. The test-specific model adapts
SQL Server defaults, data types and filtered indexes for SQLite. SQLite does
not reproduce SQL Server rowversion updates, locking, collation, SQL-specific
queries, or migrations. Tests of those features require SQL Server. For
example, evidence and escalation uploads currently use SQL Server locking
hints and cannot be exercised by this fixture. Set `RAAMS_TEST_SQL_CONNECTION`
to a SQL Server instance on which the test login can create and delete databases
to run SQL Server HTTP integration tests. They create uniquely named databases,
apply the real migrations, and delete those databases afterwards.
