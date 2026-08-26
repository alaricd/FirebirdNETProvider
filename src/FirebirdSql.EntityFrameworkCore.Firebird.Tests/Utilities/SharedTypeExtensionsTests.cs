using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Utilities;

// SharedTypeExtensions (src/FirebirdSql.EntityFrameworkCore.Firebird/Utilities/SharedTypeExtensions.cs)
// is EF Core's vendored internal reflection-helper file, `internal static class SharedTypeExtensions`
// in the top-level `System` namespace. Reachable here only via the InternalsVisibleTo grant added to
// Properties/EntityFrameworkCoreAssemblyInfo.cs. Each extension method is exercised directly against
// real CLR types -- no EF pipeline, no database, no fakeDb involved.
public class SharedTypeExtensionsTests
{
	[Test]
	public void UnwrapNullableType_returns_the_underlying_type_for_Nullable_of_T()
	{
		Assert.That(typeof(int?).UnwrapNullableType(), Is.EqualTo(typeof(int)));
		Assert.That(typeof(int).UnwrapNullableType(), Is.EqualTo(typeof(int)));
		Assert.That(typeof(string).UnwrapNullableType(), Is.EqualTo(typeof(string)));
	}

	[Test]
	public void IsNullableValueType_is_true_only_for_Nullable_of_T()
	{
		Assert.That(typeof(int?).IsNullableValueType(), Is.True);
		Assert.That(typeof(int).IsNullableValueType(), Is.False);
		Assert.That(typeof(string).IsNullableValueType(), Is.False);
	}

	[Test]
	public void IsNullableType_is_true_for_reference_types_and_Nullable_of_T()
	{
		Assert.That(typeof(string).IsNullableType(), Is.True);
		Assert.That(typeof(int?).IsNullableType(), Is.True);
		Assert.That(typeof(int).IsNullableType(), Is.False);
	}

	[Test]
	public void IsValidEntityType_requires_a_non_array_class()
	{
		Assert.That(typeof(Customer).IsValidEntityType(), Is.True);
		Assert.That(typeof(int[]).IsValidEntityType(), Is.False);
		Assert.That(typeof(int).IsValidEntityType(), Is.False);
	}

	[Test]
	public void IsPropertyBagType_detects_IDictionary_of_string_object()
	{
		Assert.That(typeof(Dictionary<string, object>).IsPropertyBagType(), Is.True);
		Assert.That(typeof(Dictionary<int, object>).IsPropertyBagType(), Is.False);
		Assert.That(typeof(List<int>).IsPropertyBagType(), Is.False);
		Assert.That(typeof(Dictionary<,>).IsPropertyBagType(), Is.False);
	}

	[Test]
	public void MakeNullable_adds_or_removes_Nullable_of_T_as_requested()
	{
		Assert.That(typeof(int).MakeNullable(), Is.EqualTo(typeof(int?)));
		Assert.That(typeof(int?).MakeNullable(), Is.EqualTo(typeof(int?)));
		Assert.That(typeof(int?).MakeNullable(nullable: false), Is.EqualTo(typeof(int)));
		Assert.That(typeof(int).MakeNullable(nullable: false), Is.EqualTo(typeof(int)));
	}

	[Test]
	public void IsNumeric_covers_integers_and_floating_point_but_not_string()
	{
		Assert.That(typeof(int).IsNumeric(), Is.True);
		Assert.That(typeof(decimal).IsNumeric(), Is.True);
		Assert.That(typeof(float).IsNumeric(), Is.True);
		Assert.That(typeof(double).IsNumeric(), Is.True);
		Assert.That(typeof(int?).IsNumeric(), Is.True);
		Assert.That(typeof(string).IsNumeric(), Is.False);
	}

	[Test]
	public void IsInteger_covers_every_integer_width_including_char_but_not_floating_point()
	{
		foreach (var integerType in new[] { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte), typeof(char) })
		{
			Assert.That(integerType.IsInteger(), Is.True, integerType.Name);
		}

		Assert.That(typeof(double).IsInteger(), Is.False);
		Assert.That(typeof(string).IsInteger(), Is.False);
	}

	[Test]
	public void IsSignedInteger_excludes_unsigned_and_byte_and_char()
	{
		Assert.That(typeof(int).IsSignedInteger(), Is.True);
		Assert.That(typeof(long).IsSignedInteger(), Is.True);
		Assert.That(typeof(short).IsSignedInteger(), Is.True);
		Assert.That(typeof(sbyte).IsSignedInteger(), Is.True);
		Assert.That(typeof(uint).IsSignedInteger(), Is.False);
		Assert.That(typeof(byte).IsSignedInteger(), Is.False);
		Assert.That(typeof(char).IsSignedInteger(), Is.False);
	}

	[Test]
	public void IsAnonymousType_recognizes_a_real_compiler_generated_anonymous_type()
	{
		var anon = new { Name = "Ada" };
		Assert.That(anon.GetType().IsAnonymousType(), Is.True);
		Assert.That(typeof(Customer).IsAnonymousType(), Is.False);
		Assert.That(typeof(int).IsAnonymousType(), Is.False);
	}

	[Test]
	public void GetAnyProperty_finds_a_declared_property_by_name_or_returns_null()
	{
		var property = typeof(Customer).GetAnyProperty(nameof(Customer.Name));
		Assert.That(property, Is.Not.Null);
		Assert.That(property.Name, Is.EqualTo(nameof(Customer.Name)));

		Assert.That(typeof(Customer).GetAnyProperty("DoesNotExist"), Is.Null);
	}

	[Test]
	public void IsInstantiable_excludes_abstract_types_interfaces_and_open_generics()
	{
		Assert.That(typeof(Customer).IsInstantiable(), Is.True);
		Assert.That(typeof(AbstractBase).IsInstantiable(), Is.False);
		Assert.That(typeof(IDisposable).IsInstantiable(), Is.False);
		Assert.That(typeof(List<>).IsInstantiable(), Is.False);
		Assert.That(typeof(List<int>).IsInstantiable(), Is.True);
	}

	enum Color
	{
		Red,
		Green,
		Blue
	}

	[Test]
	public void UnwrapEnumType_returns_the_underlying_integral_type_nullable_or_not()
	{
		Assert.That(typeof(Color).UnwrapEnumType(), Is.EqualTo(typeof(int)));
		Assert.That(typeof(Color?).UnwrapEnumType(), Is.EqualTo(typeof(int?)));
		Assert.That(typeof(Customer).UnwrapEnumType(), Is.EqualTo(typeof(Customer)));
	}

	[Test]
	public void GetSequenceType_returns_the_element_type_or_throws_for_a_non_sequence()
	{
		Assert.That(typeof(List<int>).GetSequenceType(), Is.EqualTo(typeof(int)));
		Assert.That(() => typeof(int).GetSequenceType(), Throws.ArgumentException);
	}

	[Test]
	public void TryGetSequenceType_returns_null_instead_of_throwing_for_a_non_sequence()
	{
		Assert.That(typeof(List<string>).TryGetSequenceType(), Is.EqualTo(typeof(string)));
		Assert.That(typeof(string[]).TryGetSequenceType(), Is.EqualTo(typeof(string)));
		Assert.That(typeof(int).TryGetSequenceType(), Is.Null);
	}

	[Test]
	public void TryGetElementType_returns_the_single_matching_generic_interface_argument()
	{
		Assert.That(typeof(List<decimal>).TryGetElementType(typeof(IEnumerable<>)), Is.EqualTo(typeof(decimal)));
		Assert.That(typeof(Customer).TryGetElementType(typeof(IEnumerable<>)), Is.Null);
		Assert.That(typeof(List<>).TryGetElementType(typeof(IEnumerable<>)), Is.Null);
	}

	[Test]
	public void IsCompatibleWith_covers_direct_assignability_and_sequence_element_compatibility()
	{
		Assert.That(typeof(object).IsCompatibleWith(typeof(Customer)), Is.True);
		Assert.That(typeof(List<int>).IsCompatibleWith(typeof(int[])), Is.True);
		Assert.That(typeof(List<int>).IsCompatibleWith(typeof(string[])), Is.False);
		Assert.That(typeof(int).IsCompatibleWith(typeof(string)), Is.False);
	}

	[Test]
	public void GetGenericTypeImplementations_finds_matching_closed_interfaces_and_base_types()
	{
		var implementations = typeof(List<int>).GetGenericTypeImplementations(typeof(IEnumerable<>)).ToList();
		Assert.That(implementations, Does.Contain(typeof(IEnumerable<int>)));

		Assert.That(typeof(List<>).GetGenericTypeImplementations(typeof(IEnumerable<>)), Is.Empty);
	}

	[Test]
	public void GetBaseTypes_walks_the_base_type_chain_excluding_the_type_itself()
	{
		var baseTypes = typeof(Derived).GetBaseTypes().ToList();
		Assert.That(baseTypes, Is.EqualTo(new[] { typeof(Base), typeof(object) }));
	}

	[Test]
	public void GetBaseTypesAndInterfacesInclusive_includes_the_type_itself_and_its_interfaces()
	{
		var all = typeof(Derived).GetBaseTypesAndInterfacesInclusive();
		Assert.That(all, Does.Contain(typeof(Derived)));
		Assert.That(all, Does.Contain(typeof(Base)));
		Assert.That(all, Does.Contain(typeof(IComparable)));
	}

	[Test]
	public void GetTypesInHierarchy_includes_the_type_itself_then_each_base_type()
	{
		var hierarchy = typeof(Derived).GetTypesInHierarchy().ToList();
		Assert.That(hierarchy, Is.EqualTo(new[] { typeof(Derived), typeof(Base), typeof(object) }));
	}

	[Test]
	public void GetDeclaredInterfaces_excludes_interfaces_already_implemented_by_the_base_type()
	{
		var declared = typeof(Derived).GetDeclaredInterfaces().ToList();
		Assert.That(declared, Does.Not.Contain(typeof(IComparable)), "IComparable is declared on Base, not Derived");
	}

	[Test]
	public void GetDeclaredConstructor_finds_a_constructor_matching_the_parameter_types_or_null()
	{
		var ctor = typeof(Customer).GetDeclaredConstructor(Type.EmptyTypes);
		Assert.That(ctor, Is.Not.Null);

		Assert.That(typeof(Customer).GetDeclaredConstructor(new[] { typeof(Guid) }), Is.Null);
	}

	[Test]
	public void GetPropertiesInHierarchy_finds_a_same_named_property_declared_at_any_level()
	{
		var properties = typeof(Derived).GetPropertiesInHierarchy(nameof(Base.BaseValue)).ToList();
		Assert.That(properties, Has.Count.EqualTo(1));
		Assert.That(properties[0].DeclaringType, Is.EqualTo(typeof(Base)));
	}

	[Test]
	public void GetMembersInHierarchy_yields_instance_properties_and_fields_up_the_type_chain()
	{
		var members = typeof(Customer).GetMembersInHierarchy().Select(m => m.Name).ToList();
		Assert.That(members, Does.Contain(nameof(Customer.Name)));

		var byName = typeof(Customer).GetMembersInHierarchy(nameof(Customer.Name)).ToList();
		Assert.That(byName, Has.Count.EqualTo(1));
	}

	[Test]
	public void GetDefaultValue_returns_null_for_reference_types_and_the_zero_value_for_value_types()
	{
		Assert.That(typeof(Customer).GetDefaultValue(), Is.Null);
		Assert.That(typeof(int).GetDefaultValue(), Is.EqualTo(0));
		Assert.That(typeof(Guid).GetDefaultValue(), Is.EqualTo(Guid.Empty));
		// DateTimeOffset is a value type but not in CommonTypeDictionary's fast path in every case
		// exercised above -- covers the Activator.CreateInstance fallback branch too, alongside a
		// type (Color, an enum) not present in CommonTypeDictionary at all.
		Assert.That(typeof(Color).GetDefaultValue(), Is.EqualTo(Color.Red));
	}

	[Test]
	public void GetConstructibleTypes_excludes_abstract_and_open_generic_types_from_a_real_assembly()
	{
		var assembly = typeof(SharedTypeExtensionsTests).Assembly;
		var constructible = assembly.GetConstructibleTypes().ToList();

		Assert.That(constructible, Has.Some.Matches<TypeInfo>(t => t.AsType() == typeof(SharedTypeExtensionsTests)));
		Assert.That(constructible, Has.None.Matches<TypeInfo>(t => t.IsAbstract));
	}

	[Test]
	public void GetLoadableDefinedTypes_returns_the_assemblys_defined_types()
	{
		var assembly = typeof(SharedTypeExtensionsTests).Assembly;
		var types = assembly.GetLoadableDefinedTypes().ToList();

		Assert.That(types, Has.Some.Matches<TypeInfo>(t => t.AsType() == typeof(SharedTypeExtensionsTests)));
	}

	[Test]
	public void TryGetElementType_returns_null_when_more_than_one_closed_interface_implementation_matches()
	{
		// MultiEnumerable implements IEnumerable<int> AND IEnumerable<string> directly -- an
		// ambiguous match for "the" IEnumerable<> element type.
		Assert.That(typeof(MultiEnumerable).TryGetElementType(typeof(IEnumerable<>)), Is.Null);
	}

	[Test]
	public void GetBaseTypesAndInterfacesInclusive_unwraps_a_nullable_value_type_and_a_closed_generic_type()
	{
		var fromNullable = typeof(int?).GetBaseTypesAndInterfacesInclusive();
		Assert.That(fromNullable, Does.Contain(typeof(int)));

		var fromClosedGeneric = typeof(List<int>).GetBaseTypesAndInterfacesInclusive();
		Assert.That(fromClosedGeneric, Does.Contain(typeof(List<>)));
	}

	[Test]
	public void DisplayName_renders_built_in_type_names()
	{
		Assert.That(typeof(int).DisplayName(), Is.EqualTo("int"));
		Assert.That(typeof(void).DisplayName(), Is.EqualTo("void"));
	}

	[Test]
	public void DisplayName_renders_a_generic_type_with_its_arguments()
	{
		Assert.That(typeof(List<int>).DisplayName(fullName: false), Is.EqualTo("List<int>"));
		Assert.That(typeof(Dictionary<string, int>).DisplayName(fullName: false), Is.EqualTo("Dictionary<string, int>"));
	}

	[Test]
	public void DisplayName_renders_Nullable_of_T_with_a_trailing_question_mark()
	{
		Assert.That(typeof(int?).DisplayName(fullName: false), Is.EqualTo("int?"));
	}

	[Test]
	public void DisplayName_renders_array_types_with_the_correct_rank()
	{
		Assert.That(typeof(int[]).DisplayName(fullName: false), Is.EqualTo("int[]"));
		Assert.That(typeof(int[,]).DisplayName(fullName: false), Is.EqualTo("int[,]"));
	}

	[Test]
	public void DisplayName_honors_fullName_and_compilable_for_a_user_defined_type()
	{
		var shortName = typeof(Customer).DisplayName(fullName: false);
		Assert.That(shortName, Is.EqualTo(nameof(Customer)));

		var fullName = typeof(Customer).DisplayName(fullName: true, compilable: false);
		Assert.That(fullName, Does.Contain(nameof(Customer)));
		Assert.That(fullName, Does.Contain("."));
	}

	[Test]
	public void DisplayName_renders_a_nested_type_using_its_declaring_type_when_compilable()
	{
		var name = typeof(Base.Nested).DisplayName(fullName: true, compilable: true);
		Assert.That(name, Does.Contain(nameof(Base)));
		Assert.That(name, Does.Contain(nameof(Base.Nested)));
	}

	[Test]
	public void DisplayName_compilable_renders_a_non_nested_generic_type_with_its_full_namespace()
	{
		var name = typeof(GenericOuter<int>).DisplayName(fullName: true, compilable: true);
		Assert.That(name, Does.StartWith(typeof(GenericOuter<>).Namespace + "."));
		Assert.That(name, Does.Contain(nameof(GenericOuter<int>)));
	}

	[Test]
	public void DisplayName_compilable_renders_a_nested_generic_type_via_its_declaring_type()
	{
		var name = typeof(GenericOuter<int>.GenericInner<string>).DisplayName(fullName: true, compilable: true);
		Assert.That(name, Does.Contain(nameof(GenericOuter<int>)));
		Assert.That(name, Does.Contain(nameof(GenericOuter<int>.GenericInner<string>)));
		Assert.That(name, Does.Contain("."), "compilable nesting is joined with '.'");
	}

	[Test]
	public void DisplayName_non_compilable_full_name_renders_a_non_nested_generic_type_with_its_namespace()
	{
		var name = typeof(GenericOuter<int>).DisplayName(fullName: true, compilable: false);
		Assert.That(name, Does.StartWith(typeof(GenericOuter<>).Namespace + "."));
	}

	[Test]
	public void DisplayName_non_compilable_full_name_renders_a_nested_generic_type_joined_with_a_plus()
	{
		var name = typeof(GenericOuter<int>.GenericInner<string>).DisplayName(fullName: true, compilable: false);
		Assert.That(name, Does.Contain("+"), "non-compilable nesting is joined with '+', matching reflection's own Type.FullName convention");
	}

	[Test]
	public void DisplayName_renders_a_nested_type_with_no_generic_arguments_of_its_own_by_its_bare_name()
	{
		// GenericOuter<T>.PlainInner is technically IsGenericType (it closes over the outer type's
		// T) but declares no generic parameters of its own, so its reflection Name has no `N
		// suffix -- the genericPartIndex <= 0 short-circuit in ProcessGenericType.
		var name = typeof(GenericOuter<int>.PlainInner).DisplayName(fullName: false, compilable: false);
		Assert.That(name, Is.EqualTo(nameof(GenericOuter<int>.PlainInner)));
	}

	[Test]
	public void GetNamespaces_yields_the_types_own_namespace_and_its_generic_arguments_namespaces()
	{
		var namespaces = typeof(List<Customer>).GetNamespaces().ToList();
		Assert.That(namespaces, Does.Contain(typeof(List<>).Namespace));
		Assert.That(namespaces, Does.Contain(typeof(Customer).Namespace));
	}

	[Test]
	public void GetNamespaces_yields_nothing_for_a_built_in_type()
	{
		Assert.That(typeof(int).GetNamespaces(), Is.Empty);
	}

	[Test]
	public void GetDefaultValueConstant_produces_a_constant_expression_carrying_the_types_default_value()
	{
		var constant = typeof(int).GetDefaultValueConstant();
		Assert.That(constant.Type, Is.EqualTo(typeof(int)));
		Assert.That(constant.Value, Is.EqualTo(0));

		var guidConstant = typeof(Guid).GetDefaultValueConstant();
		Assert.That(guidConstant.Value, Is.EqualTo(Guid.Empty));
	}

	sealed class Customer
	{
		public string Name { get; set; } = string.Empty;
	}

	sealed class MultiEnumerable : IEnumerable<int>, IEnumerable<string>
	{
		public IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();

		IEnumerator<string> IEnumerable<string>.GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	sealed class GenericOuter<T>
	{
		public sealed class GenericInner<TInner>
		{
		}

		public sealed class PlainInner
		{
		}
	}

	abstract class AbstractBase
	{
	}

	class Base : IComparable
	{
		public int BaseValue { get; set; }

		public sealed class Nested
		{
		}

		public int CompareTo(object obj) => 0;
	}

	sealed class Derived : Base
	{
	}
}
