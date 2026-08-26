using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.stormgate.EntityFrameworkCore;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.StormGate;

// Tier 1 of pengdows.crud's two-tier compatibility model (see EfProviders.cs in
// pengdows.crud/pengdows.stormgate.EntityFrameworkCore.MultiProvider.Tests): does the Firebird EF
// Core provider accept an externally supplied fakeDbConnection at all via UseFirebird(connection)
// -- never its own FbConnection -- and does StormGate's DbConnectionInterceptor-based admission
// control, which fires purely on EF Core's connection open/close lifecycle and never touches
// anything Firebird-specific, actually gate concurrent opens and release permits correctly for
// this provider? No real Firebird server anywhere in this file.
public class StormGateEfInteropTests
{
	[Test]
	public async Task Second_open_is_gated_while_the_first_connection_is_still_open()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var gate = global::pengdows.stormgate.StormGate.Create(factory, "server=fake;database=fake;", maxConcurrentOpens: 1, acquireTimeout: TimeSpan.FromMilliseconds(150));
		var interceptor = new StormGateConnectionInterceptor(gate);

		await using var context1 = CreateContext(factory, interceptor);
		await using var context2 = CreateContext(factory, interceptor);

		await context1.Database.OpenConnectionAsync();

		var thrown = await Catch(() => context2.Database.OpenConnectionAsync());
		Assert.That(FindStormGateSaturationTimeout(thrown), Is.Not.Null, $"Expected a storm-gate saturation TimeoutException, got: {thrown}");

		await context1.Database.CloseConnectionAsync();

		await context2.Database.OpenConnectionAsync();
		await context2.Database.CloseConnectionAsync();
	}

	[Test]
	public async Task Permit_is_released_when_the_physical_open_fails()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var gate = global::pengdows.stormgate.StormGate.Create(factory, "server=fake;database=fake;", maxConcurrentOpens: 1, acquireTimeout: TimeSpan.FromMilliseconds(150));
		var interceptor = new StormGateConnectionInterceptor(gate);

		var failingConnection = (fakeDbConnection)factory.CreateConnection()!;
		failingConnection.BreakConnection(skipFirst: true);

		var failingOptions = new DbContextOptionsBuilder<ProbeDbContext>()
			.UseFirebird(failingConnection)
			.UseStormGate(interceptor)
			.Options;

		await using (var failingContext = new ProbeDbContext(failingOptions))
		{
			Assert.That(await Catch(() => failingContext.Database.OpenConnectionAsync()), Is.Not.Null);
		}

		// Would time out if the failed open above had leaked its permit.
		await using var context = CreateContext(factory, interceptor);
		await context.Database.OpenConnectionAsync();
		await context.Database.CloseConnectionAsync();
	}

	[Test]
	public async Task Permit_stays_held_when_only_the_query_is_canceled_on_a_caller_opened_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var gate = global::pengdows.stormgate.StormGate.Create(factory, "server=fake;database=fake;", maxConcurrentOpens: 1, acquireTimeout: TimeSpan.FromMilliseconds(150));
		var interceptor = new StormGateConnectionInterceptor(gate);

		var connection = (fakeDbConnection)factory.CreateConnection()!;
		using var cts = new CancellationTokenSource();
		connection.EnqueueReaderResult(
			new[]
			{
				new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "Ada" },
				new Dictionary<string, object> { ["Id"] = 2, ["Name"] = "Grace" }
			},
			cancelAfterRowCount: 1,
			cts);

		var options = new DbContextOptionsBuilder<BlogContext>()
			.UseFirebird(connection)
			.UseStormGate(interceptor)
			.Options;

		await using var context = new BlogContext(options);
		await context.Database.OpenConnectionAsync();

		Assert.That(await Catch(() => context.Blogs.ToListAsync(cts.Token)), Is.InstanceOf<OperationCanceledException>());
		Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));

		// The caller-opened connection is still open regardless of the canceled query, so a
		// concurrent open attempt must still find the gate saturated.
		await using var probe = CreateContext(factory, interceptor);
		Assert.That(await Catch(() => probe.Database.OpenConnectionAsync()), Is.InstanceOf<TimeoutException>());

		// Only explicitly closing the connection releases the permit.
		await context.Database.CloseConnectionAsync();
		await probe.Database.OpenConnectionAsync();
		await probe.Database.CloseConnectionAsync();
	}

	[Test]
	public async Task Genuinely_concurrent_opens_are_admitted_together_not_serialized()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var gate = global::pengdows.stormgate.StormGate.Create(factory, "server=fake;database=fake;", maxConcurrentOpens: 2, acquireTimeout: TimeSpan.FromMilliseconds(150));
		var interceptor = new StormGateConnectionInterceptor(gate);

		var connection1 = (fakeDbConnection)factory.CreateConnection()!;
		var connection2 = (fakeDbConnection)factory.CreateConnection()!;
		var connection3 = (fakeDbConnection)factory.CreateConnection()!;

		var openGate1 = connection1.SetOpenGate();
		var openGate2 = connection2.SetOpenGate();

		await using var context1 = CreateContext(connection1, interceptor);
		await using var context2 = CreateContext(connection2, interceptor);
		await using var context3 = CreateContext(connection3, interceptor);

		var openTask1 = context1.Database.OpenConnectionAsync();
		var openTask2 = context2.Database.OpenConnectionAsync();

		// Both permits are granted and both opens are simultaneously paused on their own gate --
		// proof the semaphore admits maxConcurrentOpens at once rather than secretly serializing.
		Assert.That(openTask1.IsCompleted, Is.False);
		Assert.That(openTask2.IsCompleted, Is.False);

		Assert.That(await Catch(() => context3.Database.OpenConnectionAsync()), Is.InstanceOf<TimeoutException>());

		openGate1.SetResult(true);
		await openTask1;
		await context1.Database.CloseConnectionAsync();

		await context3.Database.OpenConnectionAsync();
		await context3.Database.CloseConnectionAsync();

		openGate2.SetResult(true);
		await openTask2;
		await context2.Database.CloseConnectionAsync();
	}

	[Test]
	public async Task Transaction_commit_reaches_the_fake_transaction_and_releases_the_permit()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var gate = global::pengdows.stormgate.StormGate.Create(factory, "server=fake;database=fake;", maxConcurrentOpens: 1, acquireTimeout: TimeSpan.FromMilliseconds(150));
		var interceptor = new StormGateConnectionInterceptor(gate);

		await using (var context = CreateContext(factory, interceptor))
		{
			await using var txn = await context.Database.BeginTransactionAsync();
			var fakeTxn = (fakeDbTransaction)txn.GetDbTransaction();

			await txn.CommitAsync();

			Assert.That(fakeTxn.CommitCallCount, Is.EqualTo(1));
			Assert.That(fakeTxn.RollbackCallCount, Is.EqualTo(0));
		}

		// Would time out if the committed transaction's connection never closed, leaking its permit.
		await using var nextContext = CreateContext(factory, interceptor);
		await nextContext.Database.OpenConnectionAsync();
		await nextContext.Database.CloseConnectionAsync();
	}

	[Test]
	public async Task Transaction_rollback_reaches_the_fake_transaction_and_releases_the_permit()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var gate = global::pengdows.stormgate.StormGate.Create(factory, "server=fake;database=fake;", maxConcurrentOpens: 1, acquireTimeout: TimeSpan.FromMilliseconds(150));
		var interceptor = new StormGateConnectionInterceptor(gate);

		await using (var context = CreateContext(factory, interceptor))
		{
			await using var txn = await context.Database.BeginTransactionAsync();
			var fakeTxn = (fakeDbTransaction)txn.GetDbTransaction();

			await txn.RollbackAsync();

			Assert.That(fakeTxn.RollbackCallCount, Is.EqualTo(1));
			Assert.That(fakeTxn.CommitCallCount, Is.EqualTo(0));
		}

		await using var nextContext = CreateContext(factory, interceptor);
		await nextContext.Database.OpenConnectionAsync();
		await nextContext.Database.CloseConnectionAsync();
	}

	static async Task<Exception> Catch(Func<Task> action)
	{
		try
		{
			await action();
			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	// Some execution strategies reclassify a TimeoutException raised during connection open and
	// wrap it, so the exact exception TYPE the caller sees can vary -- the interceptor's
	// saturation TimeoutException must still be traceable as the root cause regardless of wrapping.
	static TimeoutException FindStormGateSaturationTimeout(Exception exception)
	{
		while (exception != null)
		{
			if (exception is TimeoutException { Message: var message } timeout
				&& message.Contains("storm gate", StringComparison.OrdinalIgnoreCase))
			{
				return timeout;
			}

			exception = exception.InnerException;
		}

		return null;
	}

	static ProbeDbContext CreateContext(fakeDbFactory factory, StormGateConnectionInterceptor interceptor)
		=> CreateContext(factory.CreateConnection()!, interceptor);

	static ProbeDbContext CreateContext(DbConnection connection, StormGateConnectionInterceptor interceptor)
	{
		var options = new DbContextOptionsBuilder<ProbeDbContext>()
			.UseFirebird(connection)
			.UseStormGate(interceptor)
			.Options;
		return new ProbeDbContext(options);
	}

	sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);

	sealed class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
	{
		public DbSet<Blog> Blogs => Set<Blog>();
	}

	sealed class Blog
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}
}
