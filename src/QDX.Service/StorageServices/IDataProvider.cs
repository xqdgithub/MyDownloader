using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QDX.Serivices.StorageServices
{
    public interface IDataProvider
    {
        public T? Get<T>(string key="");
        public void Set<T>(string key = "", T? value = default, TimeSpan expireTime = default);
        public void Delete<T>(string key = "");
        public bool ContaionsKey(string key);
        public bool TrySet<T>(string key="",T? value=default,TimeSpan expireTime=default);
        public bool TryDelete<T>(string key = "");
    }
}
