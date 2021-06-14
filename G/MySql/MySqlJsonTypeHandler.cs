using System;
using System.Data;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G.MySql
{
    public class MySqlJsonTypeHandler : SqlMapper.TypeHandler<object>
    {
        public override object Parse(object value)
        {
            var json = value.ToString();
            return JsonConvert.DeserializeObject(json);
        }

        public override void SetValue(IDbDataParameter parameter, object value)
        {
            parameter.Value = JsonConvert.SerializeObject(value);
        }
    }
}
