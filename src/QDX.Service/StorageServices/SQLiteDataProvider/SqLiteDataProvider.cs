using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using QDX.Serivices.StorageServices.DBDataProvider;
using QDX.Serivices.StorageServices.SQLiteDataProvider.Entities;
using QDX.Service;

namespace QDX.Serivices.StorageServices.SQLiteDataProvider
{
    public sealed class SqLiteDataProvider : IDataProvider,IDisposable
    {
        private SQLiteDataContext dbContext; 
        private const string DefaultDb = "./default.db";
        private string sqliteDataBase="";
        public string SqliteDataBase { get
            {
                return sqliteDataBase;
            }
            set
            {
                sqliteDataBase = value;
                if (!File.Exists(sqliteDataBase))
                {
                    CopyDb(sqliteDataBase, default);
                }
                dbContext = new SQLiteDataContext()
                {
                    ConnectionString = GetConnectionString(),
                };
            }
        }
        public SqLiteDataProvider()
        {
            if (!File.Exists(DefaultDb))
            {
                CopyDb(DefaultDb);
            }
            if (dbContext == null)
            {
                dbContext = new SQLiteDataContext()
                {
                    ConnectionString = GetConnectionString(),
                };
            }
            SqliteDataBase=DefaultDb;
        }

        private static ConcurrentDictionary<KeyType, object?> cachedData = new ConcurrentDictionary<KeyType, object?>();
        private bool disposedValue;

        private string GetConnectionString()
        {
            string connectionString = $"Data Source={SqliteDataBase}";
            return connectionString;
        }
        public T? Get<T>(string key = "")
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            KeyType keyType = new KeyType()
            {
                db = SqliteDataBase,
                key = key,
                type = typeof(T)
            };
            if (cachedData.ContainsKey(keyType) && cachedData[keyType] != null)
            {
                return DeepCopy<T>(cachedData[keyType]);
            }
            T? result = default;
            var data = dbContext.DataItems.FirstOrDefault(x => x.Key == key);
            if (data != null)
            {
                var json = data.Value;
                result = JsonSerializer.Deserialize<T>(json);
            }
            cachedData.TryAdd(keyType, result);
            return DeepCopy<T>(result);
        }

        private static T? DeepCopy<T>(object? value)
        {
            if (value == null) return default;
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(json);
        }

        public void Set<T>(string key = "", T? value = default, TimeSpan expireTime = default)
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            KeyType keyType = new KeyType()
            {
                db = SqliteDataBase,
                key = key,
                type = typeof(T)
            };

            var json = JsonSerializer.Serialize(value);
            var item = dbContext?.DataItems.FirstOrDefault(x => x.Key == key);
            if (item == null)
            {
                dbContext?.DataItems.Add(new DataItem()
                {
                    Key = key,
                    Value = json,
                    Type = 0
                });
            }
            else
            {
                item.Value = json;
            }
            dbContext?.SaveChanges();
            cachedData[keyType] = DeepCopy<T>(value);
        }
        public void Delete<T>(string key = "")
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            KeyType keyType = new KeyType()
            {
                db = SqliteDataBase,
                key = key,
                type = typeof(T)
            };
            if (cachedData.ContainsKey(keyType))
            {
                cachedData.Remove(keyType, out _);
            }
            var obj = dbContext.DataItems.First(x => x.Key == key);
            if (obj != null)
            {
                dbContext.DataItems.Remove(obj);
                dbContext.SaveChanges() ;
            }
        }
        public bool TryDelete<T>(string key = "")
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            throw new NotImplementedException();
        }

        public bool CopyDb(string targetPath, Stream? sourceDb=default)
        {
            sourceDb = sourceDb ??new MemoryStream(DbTemoleteResource.dbtemplete);
            if (sourceDb == null)
            {
                return false;
            }
            var dir=Path.GetDirectoryName(targetPath)??"";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using FileStream fs=File.Create(targetPath);
            sourceDb.CopyTo(fs);
            return true;
        }

        public bool TrySet<T>(string key = "", T? value = default, TimeSpan expireTime = default)
        {
            key = string.IsNullOrEmpty(key) ? typeof(T).Name : key;
            throw new NotImplementedException();
        }

        public bool ContaionsKey(string key)
        {
          return  dbContext.DataItems.Any(x => x.Key == key);
        }

        public void Dispose()
        {
            ((IDisposable)dbContext)?.Dispose();
        }
    }
    public struct KeyType : IEquatable<KeyType>
    {
        public string db;
        public string key;
        public Type type;

        public bool Equals(KeyType other)
        {
            return db == other.db && key == other.key && type == other.type;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(db, key, type);
        }
    }
}
