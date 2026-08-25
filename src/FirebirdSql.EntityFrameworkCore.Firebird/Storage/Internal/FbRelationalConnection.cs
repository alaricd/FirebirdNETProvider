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

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Storage.Internal;

public class FbRelationalConnection : RelationalConnection, IFbRelationalConnection
{
	readonly DbProviderFactory _providerFactory;

	public FbRelationalConnection(RelationalConnectionDependencies dependencies, DbProviderFactory providerFactory)
		: base(dependencies)
	{
		_providerFactory = providerFactory;
	}

	protected override DbConnection CreateDbConnection()
	{
		var connection = _providerFactory.CreateConnection();
		connection.ConnectionString = ConnectionString;
		return connection;
	}
}
