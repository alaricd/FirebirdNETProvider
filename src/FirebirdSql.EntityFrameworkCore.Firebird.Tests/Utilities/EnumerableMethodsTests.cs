using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Utilities;

// EnumerableMethods (src/FirebirdSql.EntityFrameworkCore.Firebird/Utilities/EnumerableMethods.cs) is
// EF Core's vendored internal `MethodInfo` lookup table for translating Sum/Average/Max/Min LINQ
// calls, `internal static class EnumerableMethods` in the `Microsoft.EntityFrameworkCore` namespace
// -- reachable via the same InternalsVisibleTo grant as SharedTypeExtensionsTests.cs. Its static
// constructor populates every MethodInfo property regardless of which one a test asks for, so most
// of this file's coverage already comes for free from the class merely being touched; these tests
// specifically exercise the type-keyed lookup helper methods themselves.
public class EnumerableMethodsTests
{
	[Test]
	public void GetSumWithSelector_resolves_the_generic_Sum_overload_for_a_known_numeric_type()
	{
		var method = EnumerableMethods.GetSumWithSelector(typeof(int));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Sum"));
	}

	[Test]
	public void GetAverageWithSelector_resolves_the_generic_Average_overload_for_a_known_numeric_type()
	{
		var method = EnumerableMethods.GetAverageWithSelector(typeof(decimal));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Average"));
	}

	[Test]
	public void GetMaxWithoutSelector_resolves_the_type_specific_overload_for_a_known_numeric_type()
	{
		var method = EnumerableMethods.GetMaxWithoutSelector(typeof(int));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Max"));
	}

	[Test]
	public void GetMaxWithoutSelector_falls_back_to_the_generic_overload_for_an_unspecialized_type()
	{
		var method = EnumerableMethods.GetMaxWithoutSelector(typeof(System.Guid));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Max"));
		Assert.That(method.IsGenericMethodDefinition, Is.True, "the generic fallback is an open generic method definition");
	}

	[Test]
	public void GetMaxWithSelector_resolves_the_type_specific_overload_for_a_known_numeric_type()
	{
		var method = EnumerableMethods.GetMaxWithSelector(typeof(int));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Max"));
	}

	[Test]
	public void GetMaxWithSelector_falls_back_to_the_generic_overload_for_an_unspecialized_type()
	{
		var method = EnumerableMethods.GetMaxWithSelector(typeof(System.Guid));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.IsGenericMethodDefinition, Is.True);
	}

	[Test]
	public void GetMinWithoutSelector_resolves_the_type_specific_overload_for_a_known_numeric_type()
	{
		var method = EnumerableMethods.GetMinWithoutSelector(typeof(int));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Min"));
	}

	[Test]
	public void GetMinWithoutSelector_falls_back_to_the_generic_overload_for_an_unspecialized_type()
	{
		var method = EnumerableMethods.GetMinWithoutSelector(typeof(System.Guid));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.IsGenericMethodDefinition, Is.True);
	}

	[Test]
	public void GetMinWithSelector_resolves_the_type_specific_overload_for_a_known_numeric_type()
	{
		var method = EnumerableMethods.GetMinWithSelector(typeof(int));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.Name, Is.EqualTo("Min"));
	}

	[Test]
	public void GetMinWithSelector_falls_back_to_the_generic_overload_for_an_unspecialized_type()
	{
		var method = EnumerableMethods.GetMinWithSelector(typeof(System.Guid));

		Assert.That(method, Is.Not.Null);
		Assert.That(method.IsGenericMethodDefinition, Is.True);
	}
}
