/*
 * CarRental.Tests - Integration Tests Collection
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;

namespace CarRental.Tests.Integration;

/// <summary>
/// Collection definition for PostgreSQL integration tests
/// Tests in this collection run sequentially to avoid database conflicts
/// </summary>
[CollectionDefinition("PostgreSQL")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}

/// <summary>
/// Shared fixture for PostgreSQL tests (currently empty, container is per-test)
/// Can be extended for shared container if needed for performance
/// </summary>
public class PostgreSqlFixture
{
}
