using System.Data.Common;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Architecture;

public class ProviderFactoryBoundaryTests
{
	[Test]
	public void EfCore_provider_does_not_bypass_ado_net_abstractions()
	{
		var sourceRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "FirebirdSql.EntityFrameworkCore.Firebird"));
		var source = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
			.Select(File.ReadAllText);

		Assert.Multiple(() =>
		{
			Assert.That(source, Has.None.Contains("new FbConnection("));
			Assert.That(source, Has.None.Contains("new FbParameter("));
			Assert.That(source, Has.None.Contains("(FbConnection)"));
			Assert.That(source, Has.None.Contains("(FbParameter)"));
		});
	}

	[Test]
	public void AddEntityFrameworkFirebird_preserves_a_registered_provider_factory()
	{
		var factory = new SubstituteProviderFactory();
		var services = new ServiceCollection();
		services.AddSingleton<DbProviderFactory>(factory);
		services.AddEntityFrameworkFirebird();

		using var serviceProvider = services.BuildServiceProvider();
		Assert.That(serviceProvider.GetRequiredService<DbProviderFactory>(), Is.SameAs(factory));
	}

	sealed class SubstituteProviderFactory : DbProviderFactory
	{ }
}
