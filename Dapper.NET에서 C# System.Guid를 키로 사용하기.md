### Dapper.NET에서 `BINARY(16)`과 함께 C# System.Guid를 키로 사용하기

```csharp
// custom type handling
// https://medium.com/dapper-net/custom-type-handling-4b447b97c620

using System;
using System.Data;
using Dapper;

namespace DapperNET.Workaround
{
    public static class BinaryGuidTypeHandlers
    {
        public static void Register()
        {
            SqlMapper.RemoveTypeMap(typeof(Guid));
            SqlMapper.RemoveTypeMap(typeof(Guid?));

            SqlMapper.AddTypeHandler(new BinaryGuidTypeHandler());
            SqlMapper.AddTypeHandler(new NullableBinaryGuidTypeHandler());
        }


        public class BinaryGuidTypeHandler : SqlMapper.TypeHandler<Guid>
        {
            public override Guid Parse(object value)
            {
                return new Guid((byte[])value);
            }

            public override void SetValue(IDbDataParameter parameter, Guid value)
            {
                parameter.Value = value.ToByteArray();
            }
        }

        public class NullableBinaryGuidTypeHandler : SqlMapper.TypeHandler<Guid?>
        {
            public override Guid? Parse(object value)
            {
                return object != null ? new Guid((byte[])value) : null;
            }

            public override void SetValue(IDbDataParameter parameter, Guid? value)
            {
                parameter.Value = value != null ? value.ToByteArray() : null;
            }
        }
    }
}
```

시스템 초기화시에 아래와 같이 호출해주면 그다음부터 Dapper.NET이 `BINARY(16)` <-> `System.Guid`간의
상호 변환이 자동으로 이루어집니다.

```csharp
void InitApp()
{
    BinaryGuidTypeHandlers.Register();
}
```
