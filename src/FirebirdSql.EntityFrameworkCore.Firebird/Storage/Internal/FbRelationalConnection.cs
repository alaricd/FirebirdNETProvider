/*
 *    The contents of this file are subject to the Initial
 *    Developer's Public License Version 1.0 (the "License");
 *    you may not use this file except in compliance with the
 *    License. You may obtain a copy of the License at
 *    https://github.com/FirebirdSQL/NETProvider/raw/master/license.txt.
 *
 *    Software distributed under the License is distributed on
 *    an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *    express or implied. See the License for the specific
 *    language governing rights and limitations under the License.
 *
 *    All Rights Reserved.
 */

//$Authors = Jiri Cincura (jiri@cincura.net)

using System;
using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore.Storage;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Storage.Internal;

public class FbRelationalConnection : RelationalConnection, IFbRelationalConnection
{
	private readonly DbProviderFactory _providerFactory;

	public FbRelationalConnection(RelationalConnectionDependencies dependencies, DbProviderFactory providerFactory)
		: base(dependencies)
	{
		_providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
	}

	protected override DbConnection CreateDbConnection()
		=> CreateConnection();

	private DbConnection CreateConnection()
	{
		var connection = _providerFactory.CreateConnection()
			?? throw new InvalidOperationException("The provider factory returned no connection.");
		connection.ConnectionString = ConnectionString;
		return connection;
	}
}
