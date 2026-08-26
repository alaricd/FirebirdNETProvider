using System;
using FirebirdSql.EntityFrameworkCore.Firebird.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Scaffolding;

// FbProviderCodeGenerator generates the "UseFirebird(...)" method-call fragment that scaffolded
// DbContext code (from `dotnet ef dbcontext scaffold`) embeds in OnConfiguring -- pure code
// generation, no database (real or fake) involved at all.
public class FbProviderCodeGeneratorTests
{
	static FbProviderCodeGenerator CreateGenerator()
		=> new(new ProviderCodeGeneratorDependencies(Array.Empty<IProviderCodeGeneratorPlugin>()));

	[Test]
	public void GenerateUseProvider_with_no_provider_options_calls_UseFirebird_with_just_the_connection_string()
	{
		var generator = CreateGenerator();

		var fragment = generator.GenerateUseProvider("server=fake;database=fake;", providerOptions: null);

		Assert.That(fragment.Method, Is.EqualTo("UseFirebird"));
		Assert.That(fragment.Arguments, Has.Count.EqualTo(1));
		Assert.That(fragment.Arguments[0], Is.EqualTo("server=fake;database=fake;"));
	}

	[Test]
	public void GenerateUseProvider_with_provider_options_nests_them_as_a_configuration_closure()
	{
		var generator = CreateGenerator();
		var providerOptions = new MethodCallCodeFragment("UseHiLo");

		var fragment = generator.GenerateUseProvider("server=fake;database=fake;", providerOptions);

		Assert.That(fragment.Method, Is.EqualTo("UseFirebird"));
		Assert.That(fragment.Arguments, Has.Count.EqualTo(2));
		Assert.That(fragment.Arguments[0], Is.EqualTo("server=fake;database=fake;"));
		Assert.That(fragment.Arguments[1], Is.InstanceOf<NestedClosureCodeFragment>());
		var closure = (NestedClosureCodeFragment)fragment.Arguments[1];
		Assert.That(closure.Parameter, Is.EqualTo("x"));
		Assert.That(closure.MethodCalls, Has.Count.EqualTo(1));
		Assert.That(closure.MethodCalls[0].Method, Is.EqualTo("UseHiLo"));
	}
}
