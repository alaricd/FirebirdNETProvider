using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Utilities;

// StringBuilderExtensions (src/FirebirdSql.EntityFrameworkCore.Firebird/Utilities/StringBuilderExtensions.cs)
// is EF Core's vendored internal helper, `internal static class StringBuilderExtensions` in the
// top-level `System.Text` namespace -- reachable here via the same InternalsVisibleTo grant added
// for SharedTypeExtensionsTests.cs.
public class StringBuilderExtensionsTests
{
	[Test]
	public void AppendJoin_strings_joins_with_the_default_separator_and_trims_the_trailing_one()
	{
		var sb = new StringBuilder();
		sb.AppendJoin(new[] { "a", "b", "c" });

		Assert.That(sb.ToString(), Is.EqualTo("a, b, c"));
	}

	[Test]
	public void AppendJoin_strings_with_empty_source_appends_nothing()
	{
		var sb = new StringBuilder("prefix");
		sb.AppendJoin(System.Array.Empty<string>());

		Assert.That(sb.ToString(), Is.EqualTo("prefix"));
	}

	[Test]
	public void AppendJoin_params_strings_joins_with_a_custom_separator()
	{
		var sb = new StringBuilder();
		sb.AppendJoin(" | ", "x", "y", "z");

		Assert.That(sb.ToString(), Is.EqualTo("x | y | z"));
	}

	[Test]
	public void AppendJoin_with_join_action_invokes_the_action_per_element()
	{
		var sb = new StringBuilder();
		sb.AppendJoin(new[] { 1, 2, 3 }, (builder, value) => builder.Append('#').Append(value), ",");

		Assert.That(sb.ToString(), Is.EqualTo("#1,#2,#3"));
	}

	[Test]
	public void AppendJoin_with_join_predicate_only_appends_the_separator_when_the_predicate_returns_true()
	{
		var sb = new StringBuilder();
		sb.AppendJoin(
			new[] { 1, 2, 3, 4 },
			(builder, value) =>
			{
				if (value % 2 != 0)
				{
					return false;
				}

				builder.Append(value);
				return true;
			},
			",");

		Assert.That(sb.ToString(), Is.EqualTo("2,4"));
	}

	[Test]
	public void AppendJoin_with_join_predicate_that_never_matches_appends_nothing()
	{
		var sb = new StringBuilder("start");
		sb.AppendJoin(new[] { 1, 3, 5 }, (builder, value) => false, ",");

		Assert.That(sb.ToString(), Is.EqualTo("start"));
	}

	[Test]
	public void AppendJoin_with_extra_parameter_threads_it_through_to_the_join_action()
	{
		var sb = new StringBuilder();
		sb.AppendJoin(new[] { "a", "b" }, "!", (builder, value, suffix) => builder.Append(value).Append(suffix), ",");

		Assert.That(sb.ToString(), Is.EqualTo("a!,b!"));
	}

	[Test]
	public void AppendBytes_renders_a_short_array_as_a_hex_literal_with_no_truncation()
	{
		var sb = new StringBuilder();
		sb.AppendBytes(new byte[] { 0x01, 0xAB, 0xFF });

		Assert.That(sb.ToString(), Is.EqualTo("'0x01ABFF'"));
	}

	[Test]
	public void AppendBytes_truncates_arrays_longer_than_32_bytes_with_an_ellipsis()
	{
		var bytes = new byte[40];
		for (var i = 0; i < bytes.Length; i++)
		{
			bytes[i] = (byte)i;
		}

		var sb = new StringBuilder();
		sb.AppendBytes(bytes);
		var rendered = sb.ToString();

		Assert.That(rendered, Does.StartWith("'0x000102"));
		Assert.That(rendered, Does.Contain("..."));
		Assert.That(rendered, Does.EndWith("'"));

		var hexCharCount = rendered.Length - "'0x".Length - "...'".Length;
		// 32 bytes rendered as two hex characters each before truncation kicks in (index > 31).
		Assert.That(hexCharCount, Is.EqualTo(32 * 2));
	}

	[Test]
	public void AppendBytes_renders_an_empty_array_as_an_empty_hex_literal()
	{
		var sb = new StringBuilder();
		sb.AppendBytes(System.Array.Empty<byte>());

		Assert.That(sb.ToString(), Is.EqualTo("'0x'"));
	}
}
