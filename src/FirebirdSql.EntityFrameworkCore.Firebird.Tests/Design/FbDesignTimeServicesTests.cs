using FirebirdSql.EntityFrameworkCore.Firebird.Design.Internal;
using FirebirdSql.EntityFrameworkCore.Firebird.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Design;

// FbDesignTimeServices is the entry point `dotnet ef` invokes to discover the provider's
// design-time services (scaffolding, code generation) -- pure DI registration, no database
// involved. Confirms the Firebird-specific implementations actually win the TryAdd race against
// whatever generic defaults EntityFrameworkRelationalDesignServicesBuilder would otherwise supply.
public class FbDesignTimeServicesTests
{
	[Test]
	public void ConfigureDesignTimeServices_registers_the_Firebird_specific_database_model_factory()
	{
		var services = new ServiceCollection();

		new FbDesignTimeServices().ConfigureDesignTimeServices(services);

		Assert.That(services, Has.Some.Matches<ServiceDescriptor>(
			d => d.ServiceType == typeof(IDatabaseModelFactory) && d.ImplementationType == typeof(FbDatabaseModelFactory)));
	}

	[Test]
	public void ConfigureDesignTimeServices_registers_the_Firebird_specific_provider_code_generator()
	{
		var services = new ServiceCollection();

		new FbDesignTimeServices().ConfigureDesignTimeServices(services);

		Assert.That(services, Has.Some.Matches<ServiceDescriptor>(
			d => d.ServiceType == typeof(IProviderConfigurationCodeGenerator) && d.ImplementationType == typeof(FbProviderCodeGenerator)));
	}

	[Test]
	public void ConfigureDesignTimeServices_registers_the_relational_annotation_code_generator()
	{
		var services = new ServiceCollection();

		new FbDesignTimeServices().ConfigureDesignTimeServices(services);

		Assert.That(services, Has.Some.Matches<ServiceDescriptor>(
			d => d.ServiceType == typeof(IAnnotationCodeGenerator) && d.ImplementationType == typeof(AnnotationCodeGenerator)));
	}
}
