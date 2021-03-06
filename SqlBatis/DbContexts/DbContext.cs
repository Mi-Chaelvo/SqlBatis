﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SqlBatis
{
    public interface IDbContext : IDisposable
    {
        IDbConnection Connection { get; }
        DbContextType DbContextType { get; }
        IXmlMapper From<T>(string id, T parameter) where T : class;
        IXmlMapper From(string id);
        IDbQuery<T> From<T>();
        void BeginTransaction();
        void BeginTransaction(IsolationLevel level);
        void Close();
        void CommitTransaction();
        IMultiResult ExecuteMultiQuery(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        IEnumerable<dynamic> ExecuteQuery(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        Task<IEnumerable<dynamic>> ExecuteQueryAsync(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null);
        IEnumerable<T> ExecuteQuery<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        int ExecuteNonQuery(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        Task<int> ExecuteNonQueryAsync(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        T ExecuteScalar<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        Task<T> ExecuteScalarAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null);
        void Open();
        Task OpenAsync();
        void RollbackTransaction();
    }
    /// <summary>
    /// Execute script
    /// </summary>
    public class DbContext : IDbContext
    {
        public DbContextState DbContextState = DbContextState.Closed;

        private readonly IXmlResovle _xmlResovle = null;

        private IDbTransaction _transaction = null;

        private readonly ITypeMapper _typeMapper = null;

        public IDbConnection Connection { get; } = null;

        public DbContextType DbContextType { get; } = DbContextType.Mysql;   

        protected virtual void OnLogging(string message, IDataParameterCollection parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
        }
        protected virtual DbContextBuilder OnConfiguring(DbContextBuilder builder)
        {
            return builder;
        }
        protected DbContext()
        {
            var builder = OnConfiguring(new DbContextBuilder());
            Connection = builder.Connection;
            _xmlResovle = builder.XmlResovle;
            _typeMapper = builder.TypeMapper ?? new TypeMapper();
            DbContextType = builder.DbContextType;
        }

        public DbContext(DbContextBuilder builder)
        {
            Connection = builder.Connection;
            _xmlResovle = builder.XmlResovle;
            _typeMapper = builder.TypeMapper ?? new TypeMapper();
            DbContextType = builder.DbContextType;
        }
        /// <summary>
        /// xml query
        /// </summary>
        public IXmlMapper From<T>(string id, T parameter) where T : class
        {
            var sql = _xmlResovle.Resolve(id, parameter);
            return new XmlMapper(this, sql, parameter);
        }
        /// <summary>
        /// xml query
        /// </summary>
        public IXmlMapper From(string id)
        {
            var sql = _xmlResovle.Resolve(id);
            return new XmlMapper(this, sql);
        }

        /// <summary>
        /// linq query
        /// </summary>
        public IDbQuery<T> From<T>()
        {
            return new DbQuery<T>(this);
        }
        /// <summary>
        /// Executes a query, returning the data typed as dynamic.
        /// </summary>
        public IEnumerable<dynamic> ExecuteQuery(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = Connection.CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                using (var reader = cmd.ExecuteReader())
                {
                    var handler = TypeConvert.GetSerializer();
                    while (reader.Read())
                    {
                        yield return handler(reader);
                    }
                }
            }
        }
        /// <summary>
        /// async Executes a query, returning the data typed as dynamic.
        /// </summary>
        public async Task<IEnumerable<dynamic>> ExecuteQueryAsync(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = (Connection as DbConnection).CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var list = new List<dynamic>();
                    var handler = TypeConvert.GetSerializer();
                    while (reader.Read())
                    {
                        list.Add(handler(reader));
                    }
                    return list;
                }
            }
        }
        /// <summary>
        /// Executes multi query.
        /// </summary>
        public IMultiResult ExecuteMultiQuery(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            var cmd = Connection.CreateCommand();
            Initialize(cmd, sql, parameter, commandTimeout, commandType);
            return new MultiResult(cmd, _typeMapper);
        }
        /// <summary>
        /// Executes a query, returning the data typed as T.
        /// </summary>
        public IEnumerable<T> ExecuteQuery<T>(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = Connection.CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                using (var reader = cmd.ExecuteReader())
                {
                    var handler = TypeConvert.GetSerializer<T>(_typeMapper, reader);
                    while (reader.Read())
                    {
                        yield return handler(reader);
                    }
                }
            }
        }
        /// <summary>
        /// Executes a query, returning the data typed as T
        /// </summary>
        public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = (Connection as DbConnection).CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var list = new List<T>();
                    var handler = TypeConvert.GetSerializer<T>(_typeMapper, reader);
                    while (await reader.ReadAsync())
                    {
                        list.Add(handler(reader));
                    }
                    return list;
                }
            }
        }

        /// <summary>
        /// Execute parameterized SQL
        /// </summary>
        public int ExecuteNonQuery(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = Connection.CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Execute a command asynchronously using Task.
        /// </summary>
        public async Task<int> ExecuteNonQueryAsync(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = (Connection as DbConnection).CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Execute parameterized SQL that selects a single value.
        /// </summary>
        public T ExecuteScalar<T>(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = Connection.CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                var result = cmd.ExecuteScalar();
                if (result is DBNull || result == null)
                {
                    return default;
                }
                return (T)Convert.ChangeType(result, typeof(T));
            }
        }

        /// <summary>
        /// Execute parameterized SQL that selects a single value.
        /// </summary>
        public async Task<T> ExecuteScalarAsync<T>(string sql, object parameter = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (var cmd = (Connection as DbConnection).CreateCommand())
            {
                Initialize(cmd, sql, parameter, commandTimeout, commandType);
                var result = await cmd.ExecuteScalarAsync();
                if (result is DBNull || result == null)
                {
                    return default;
                }
                return (T)Convert.ChangeType(result, typeof(T));
            }
        }
        /// <summary>
        /// begin transaction
        /// </summary>
        /// <returns></returns>
        public void BeginTransaction()
        {
            _transaction = Connection.BeginTransaction();
            OnLogging("begin transaction");
        }

        /// <summary>
        /// begin transaction
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public void BeginTransaction(IsolationLevel level)
        {
            _transaction = Connection.BeginTransaction(level);
            OnLogging("begin transaction isolationLevel = " + level);
        }

        /// <summary>
        /// close connection
        /// </summary>
        public void Close()
        {
            _transaction?.Dispose();
            Connection?.Dispose();
            DbContextState = DbContextState.Closed;
            OnLogging("colse connection");
        }

        /// <summary>
        /// commit transaction
        /// </summary>
        public void CommitTransaction()
        {
            _transaction?.Commit();
            DbContextState = DbContextState.Commit;
            OnLogging("commit transaction");
        }

        /// <summary>
        /// open connection
        /// </summary>
        public void Open()
        {
            Connection?.Open();
            DbContextState = DbContextState.Open;
            OnLogging("open connection");
        }

        /// <summary>
        /// open connection
        /// </summary>
        /// <returns></returns>
        public async Task OpenAsync()
        {
            await (Connection as DbConnection).OpenAsync();
            DbContextState = DbContextState.Open;
            OnLogging("open connection");
        }

        /// <summary>
        /// rollback transaction
        /// </summary>
        public void RollbackTransaction()
        {
            _transaction?.Rollback();
            DbContextState = DbContextState.Rollback;
            OnLogging("rollback");

        }
        /// <summary>
        /// handler command
        /// </summary>      
        private void Initialize(IDbCommand cmd, string sql, object parameter, int? commandTimeout = null, CommandType? commandType = null)
        {
            var dbParameters = new List<IDbDataParameter>();
            cmd.Transaction = _transaction;
            cmd.CommandText = sql;
            if (commandTimeout.HasValue)
            {
                cmd.CommandTimeout = commandTimeout.Value;
            }
            if (commandType.HasValue)
            {
                cmd.CommandType = commandType.Value;
            }
            if (parameter is IDbDataParameter)
            {
                dbParameters.Add(parameter as IDbDataParameter);
            }
            else if (parameter is IEnumerable<IDbDataParameter> parameters)
            {
                dbParameters.AddRange(parameters);
            }
            else if (parameter is Dictionary<string, object> keyValues)
            {
                foreach (var item in keyValues)
                {
                    var param = CreateParameter(cmd, item.Key, item.Value);
                    dbParameters.Add(param);
                }
            }
            else if (parameter != null)
            {
                var handler = TypeConvert.GetDeserializer(parameter.GetType());
                var values = handler(parameter);
                foreach (var item in values)
                {
                    var param = CreateParameter(cmd, item.Key, item.Value);
                    dbParameters.Add(param);
                }
            }
            if (dbParameters.Count > 0)
            {
                foreach (IDataParameter item in dbParameters)
                {
                    item.Value = item.Value ?? DBNull.Value;
                    var pattern = $@"in\s+([\@,\:,\?]?{item.ParameterName})";
                    var options = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Multiline;
                    if (cmd.CommandText.IndexOf("in", StringComparison.OrdinalIgnoreCase) != -1 && Regex.IsMatch(cmd.CommandText, pattern, options))
                    {
                        var name = Regex.Match(cmd.CommandText, pattern, options).Groups[1].Value;
                        var list = new List<object>();
                        if (item.Value is IEnumerable<object> || item.Value is Array)
                        {
                            list = (item.Value as IEnumerable).Cast<object>().Where(a => a != null && a != DBNull.Value).ToList();
                        }
                        else
                        {
                            list.Add(item.Value);
                        }
                        if (list.Count() > 0)
                        {
                            cmd.CommandText = Regex.Replace(cmd.CommandText, name, $"({string.Join(",", list.Select(s => $"{name}{list.IndexOf(s)}"))})");
                            foreach (var iitem in list)
                            {
                                var key = $"{item.ParameterName}{list.IndexOf(iitem)}";
                                var param = CreateParameter(cmd, key, iitem);
                                cmd.Parameters.Add(param);
                            }
                        }
                        else
                        {
                            cmd.CommandText = Regex.Replace(cmd.CommandText, name, $"(SELECT 1 WHERE 1 = 0)");
                        }
                    }
                    else
                    {
                        cmd.Parameters.Add(item);
                    }
                }
            }
            OnLogging(cmd.CommandText, cmd.Parameters, commandTimeout, commandType);
        }

        /// <summary>
        /// create parameter
        /// </summary>
        /// <returns></returns>
        private IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            return parameter;
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            Connection?.Dispose();
        }
    }
}
