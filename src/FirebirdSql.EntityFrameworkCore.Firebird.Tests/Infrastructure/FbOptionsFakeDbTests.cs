using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Infrastructure;

// FbOptions.Validate only throws when a caller reuses the SAME internal service provider (via
// UseInternalServiceProvider) across two DbContext configurations that disagree on
// WithExplicitParameterTypes/WithExplicitStringLiteralTypes -- these are singleton options baked
// into that one service provider at first use, so a later, differently-configured context sharing
// it is a genuine, real misconfiguration EF Core itself asks providers to detect. Building each
// context with its OWN internal service provider (the normal case, and what every other fakeDb
// test in this project does) never reaches Validate's throw branches at all.
public class FbOptionsFakeDbTests
{
	[Test]
	public void Validate_throws_when_a_shared_internal_service_provider_sees_a_different_ExplicitParameterTypes()
	{
		var serviceProvider = new ServiceCollection().AddEntityFrameworkFirebird().BuildServiceProvider();

		using (var first = new ProbeContext(BuildOptions(serviceProvider, explicitParameterTypes: true)))
		{
			_ = first.Model; // forces FbOptions.Initialize for this service provider
		}

		Assert.That(
			() =>
			{
				using var second = new ProbeContext(BuildOptions(serviceProvider, explicitParameterTypes: false));
				_ = second.Model;
			},
			Throws.InvalidOperationException);
	}

	[Test]
	public void Validate_throws_when_a_shared_internal_service_provider_sees_a_different_ExplicitStringLiteralTypes()
	{
		var serviceProvider = new ServiceCollection().AddEntityFrameworkFirebird().BuildServiceProvider();

		using (var first = new ProbeContext(BuildOptions(serviceProvider, explicitStringLiteralTypes: true)))
		{
			_ = first.Model;
		}

		Assert.That(
			() =>
			{
				using var second = new ProbeContext(BuildOptions(serviceProvider, explicitStringLiteralTypes: false));
				_ = second.Model;
			},
			Throws.InvalidOperationException);
	}

	static DbContextOptions<ProbeContext> BuildOptions(IServiceProvider serviceProvider, bool explicitParameterTypes = true, bool explicitStringLiteralTypes = true)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var builder = new DbContextOptionsBuilder<ProbeContext>()
			.UseInternalServiceProvider(serviceProvider)
			.UseFirebird(connection, fb => fb
				.WithExplicitParameterTypes(explicitParameterTypes)
				.WithExplicitStringLiteralTypes(explicitStringLiteralTypes));
		return builder.Options;
	}

	sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);
}

// FbServiceCollectionExtensions.AddFirebird<TContext> -- the ASP.NET-Core-style DI registration
// entry point (as opposed to AddEntityFrameworkFirebird, already covered by
// Architecture/ProviderFactoryBoundaryTests.cs) -- wires a connection STRING through
// AddDbContext<TContext>, so the real FbConnection/DbProviderFactory resolution behavior only
// shows up once the context is actually resolved and its (lazily created) DbConnection is
// inspected, not merely by checking the service registration exists.
public class FbServiceCollectionExtensionsAddFirebirdTests
{
	[Test]
	public void AddFirebird_registers_a_resolvable_DbContext_configured_for_Firebird()
	{
		var services = new ServiceCollection();
		services.AddFirebird<ProbeContext>("server=fake;database=fake;user=fake;password=fake;");
		using var serviceProvider = services.BuildServiceProvider();

		using var scope = serviceProvider.CreateScope();
		using var context = scope.ServiceProvider.GetRequiredService<ProbeContext>();

		Assert.That(context.Database.GetDbConnection().ConnectionString, Is.EqualTo("server=fake;database=fake;user=fake;password=fake;"));
	}

	sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);
}
