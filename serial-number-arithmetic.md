# Serial number arithmetic

`sequence number` 대소 비교시에 `overflow` 문제를 피해가는 간단한 코드입니다.

`unsigned` 타입의 카운터 변수가 최대치를 넘어서 `overflow`가 발생했을 경우, 대소 비교가 잘못되는 문제가 있습니다. 이러한 문제를 피해가는 간단한 코드입니다.

자세한 내용은 [참고문서](https://en.wikipedia.org/wiki/Serial_number_arithmetic)를 참고하세요.

### C#

```csharp
public class SeqNumberHelper
{
    // return (x < y)
    public static bool Less(uint x, uint y)
    {
        return (int)(y - x) > 0;
    }

    // return (x <= y)
    public static bool LessOrEqual(uint x, uint y)
    {
        return x == y || (int)(y - x) > 0;
    }

    // return (x > y)
    public static bool Greater(uint x, uint y)
    {
        return !LessOrEqual(x, y);
    }
}
```

### C++

```cpp
class SeqNumberHelper {
 public:
  // return (x < y)
  inline static bool Less(uint32_t x, uint32_t y) {
    return (int32_t)(y - x) > 0;
  }

  // return (x <= y)
  inline static bool LessOrEqual(uint32_t x, uint32_t y) {
    return x == y || (int32_t)(y - x) > 0;
  }

  // return (x > y)
  inline static bool Greater(uint32_t x, uint32_t y) {
    return !LessOrEqual(x, y);
  }
};
```
