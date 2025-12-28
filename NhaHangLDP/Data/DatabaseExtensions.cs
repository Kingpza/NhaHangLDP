using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;

namespace NhaHangLDP.Data
{
    /// <summary>
    /// Extension methods để tương thích với EF6 Database.SqlQuery pattern
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Execute raw SQL query and return results (similar to EF6 Database.SqlQuery)
        /// Works for both class types and value types
        /// Use this instead of EF Core's SqlQueryRaw for better compatibility
        /// </summary>
        public static List<T> SqlQuery<T>(this DatabaseFacade database, string sql, params object[] parameters)
        {
            var connection = database.GetDbConnection();
            var wasOpen = connection.State == ConnectionState.Open;
            
            try
            {
                if (!wasOpen) connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = CommandType.Text;
                
                // Add parameters
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = $"@p{i}";
                    param.Value = parameters[i] ?? DBNull.Value;
                    command.Parameters.Add(param);
                }
                
                var results = new List<T>();
                using var reader = command.ExecuteReader();
                
                var type = typeof(T);
                var isValueType = type.IsValueType || type == typeof(string);
                
                if (isValueType)
                {
                    // Handle scalar types (int, decimal, string, etc.)
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            var value = reader.GetValue(0);
                            results.Add(ConvertValue<T>(value));
                        }
                        else
                        {
                            results.Add(default);
                        }
                    }
                }
                else
                {
                    // Handle complex types
                    var properties = type.GetProperties()
                        .Where(p => p.CanWrite)
                        .ToList();
                        
                    while (reader.Read())
                    {
                        var item = Activator.CreateInstance<T>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            var property = properties.FirstOrDefault(p => 
                                p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                                
                            if (property != null && !reader.IsDBNull(i))
                            {
                                var value = reader.GetValue(i);
                                try
                                {
                                    SetPropertyValue(property, item, value);
                                }
                                catch { }
                            }
                        }
                        results.Add(item);
                    }
                }
                
                return results;
            }
            finally
            {
                if (!wasOpen && connection.State == ConnectionState.Open) 
                    connection.Close();
            }
        }

        private static T ConvertValue<T>(object value)
        {
            var type = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            
            if (value == null || value == DBNull.Value)
                return default;
                
            if (underlyingType == typeof(int))
                return (T)(object)Convert.ToInt32(value);
            if (underlyingType == typeof(decimal))
                return (T)(object)Convert.ToDecimal(value);
            if (underlyingType == typeof(double))
                return (T)(object)Convert.ToDouble(value);
            if (underlyingType == typeof(float))
                return (T)(object)Convert.ToSingle(value);
            if (underlyingType == typeof(bool))
                return (T)(object)Convert.ToBoolean(value);
            if (underlyingType == typeof(string))
                return (T)(object)value.ToString();
            if (underlyingType == typeof(DateTime))
                return (T)(object)Convert.ToDateTime(value);
            if (underlyingType == typeof(long))
                return (T)(object)Convert.ToInt64(value);
                
            return (T)value;
        }
        
        private static void SetPropertyValue(System.Reflection.PropertyInfo property, object obj, object value)
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            
            if (propertyType == typeof(bool) && value is int intVal)
                property.SetValue(obj, intVal != 0);
            else if (propertyType == typeof(bool) && value is short shortVal)
                property.SetValue(obj, shortVal != 0);
            else if (propertyType == typeof(bool) && value is byte byteVal)
                property.SetValue(obj, byteVal != 0);
            else if (propertyType == typeof(TimeSpan) && value is TimeSpan ts)
                property.SetValue(obj, ts);
            else if (propertyType == typeof(DateOnly) && value is DateTime dt)
                property.SetValue(obj, DateOnly.FromDateTime(dt));
            else if (propertyType == typeof(TimeOnly) && value is TimeSpan ts2)
                property.SetValue(obj, TimeOnly.FromTimeSpan(ts2));
            else if (propertyType.IsEnum)
                property.SetValue(obj, Enum.Parse(propertyType, value.ToString()));
            else
                property.SetValue(obj, Convert.ChangeType(value, propertyType));
        }
    }
}
