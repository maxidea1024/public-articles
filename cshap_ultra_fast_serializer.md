## C#에서 초고속 시리얼라이제이션

C++ 에서는 조금만 궁리를 하면 고속 시리얼라이져를 구현하는것은 어렵지 않습니다. 반면, Managed 언어인 C#에서 고속 시리얼라이져를 구현하는것은 쉬운 과제는 아닙니다.

개인적으로 `C/C++` 만 30년 이상 다루다 보니 `C#` 환경에서 메모리 버퍼를 직접 다루는게 쉽지 않아 손발이 묶인 기분을 한동안 가질 수 밖에 없었습니다.

가장 근본적인 이유는 포인터를 직접 다루는게 쉽지 않다는데 있습니다.

C#에서도 `unsafe`, `fixed`를 사용해서 어느정도는 개선할 수 있습니다. 이에 대한 팁들에 대해서 설명하고자 합니다. 어떠한 경우에도 `C/C++` 보다 나은 성능을 달성할수는 없을것이지만, 최대한 개선해보고자 노력하였고 관련된 내용에 대해서 얘기하고자 합니다.

#### 들어가기에 앞서
- 모든 바이너리 인코딩은 `little-endian`이라고 가정하겠습니다.
- 효율적인 버퍼 관리에 대해서는 다루지 않습니다. (`ArrayPool` 이나 `Span` 등을 적용 활용하기실 권해 드립니다.)
- `Writer`, `Reader` 관련한 코드는 가급적 쉬운 설명을 위해서 개념적으로만 기술합니다.

### 1. float, double 시리얼라이제이션

```csharp
void WriteFloat(Writer writer, float value)
{
    // `BitConverter`를 통해서 byte-array로 변환합니다.
    var bytes = BitConverter.GetBytes(value);

    // 기본 인코딩이 Little-endian이므로, little-endian이 아니면 바이트 오더를 뒤짚어 줘야합니다.
    if (!BitConverter.IsLittleEndian)
        Array.Reverse(bytes);

    // byte-array를 버퍼에 기록합니다. (단순한 메모리 복사입니다.)
    writer.WriteRawBytes(bytes);
}
```

일반적인 코드입니다. 무엇이 문제일까요?

우선, `BitConverter.GetBytes()`에서 변환된 결과를 담고 있는 `byte-array` 객체가 임시로 할당되고 있습니다. 두번째로 `endian` 이슈로 인해서 `byte-swapping`이 일어날 수 있습니다.
`endian` 이슈는 제거할 수 없지만, 첫번째 임시 객체의 생성은 제거할 수 있습니다.

```csharp
[StructLayout(LayoutKind.Explicit)]
private struct I2F // `heap`에 전급하는것을 피하기 위해, 반듯이 `sturct` 타입으로 선언합니다.
{
    [FieldOffset(0)]
    public uint I;

    [FieldOffset(0)]
    public float F;
}

// 버퍼에 float 쓰기
void WriteFloat(Writer writer, float value)
{
    I2F i2f = new I2F { F = value };

    // uint 값을 버퍼에 기록합니다. (little-endian)
    writer.WriteFixed32(i2f.I);
}

// 버퍼에서 float 읽어오기
void ReadFloat(Reader reader, out float value)
{
    // uint 값을 버퍼에서 읽어옵니다. (little-endian)
    reader.ReadFixed32(out uint u);

    I2F i2f = new I2F { I = u };
    value = i2f.F;
}
```

메모리 레이아웃을 동일하게 잡아주어 `C/C++`의 공용체와 유사한 형태로 처리하여, 메모리 할당을 제거하였습니다.

이와 관련한 팁들은 인터넷 검색을 통하면 참고 넘치므로, 한번 찾아보시는것도 좋을듯 싶습니다.


### 2. string 시리얼라이제이션

제일 빈번하게 사용하는 `built-in type` 중에 하나는 `string`일 것입니다. `string` 타입 시리얼라이징을 최적화 한다면, 전체적인 성능 향상에 적지 않은 개선을 할수 있을겁니다.

기본적으로 `C#`은 `C/C++`과는 달리 유니코드(UTF16)를 사용합니다. 대개의 경우 시리얼라이징시에 `UTF8` 형태로 변환을 거치게 됩니다. 이때 필연적으로 변환을 위한 메모리 할당이 이뤄지는데 이부분을 제거하는 팁에 대해서 설명해 보도록 하겠습니다. 극단의 최적화를 달성하기 위해서 `unsafe`, `fixed`를 사용하여 구현해 보도록 하겠습니다.

들어가기에 앞서서 문자열 저장시 저는 다음과 같은 형태로 처리하고 있습니다.

```
문자열 = 문자열길이(varint: 1~5바이트)  +  UTF8Body
```

문자열의 길이를 가변으로 하는 이유는 문자열의 길이는 매우 다양하기 때문입니다. `1바이트`로 고정할 경우에는 `최대 255 바이트` 까지의 짧은 문자열 밖에는 처리를 못하고, `4바이트`로 고정할 경우에는 낭비가 좀 심해질 수 있습니다. 이에 대응하기 위해서 `base-128 encoding` 방식인 `varint`를 사용하여 카운터를 처리합니다. 고정 길이가 아닌 덕분에 처리가 조금 까다로워집니다. 이와 관련된 부분은 아래에서 상세하게 다룰 것이니 우선은 문자열 바이트 데이터 앞에 바이트 길이가 들어간다는 정도로만 알아두시면 좋습니다.

`varint`에 대한 자세한 사항은 [여기](https://en.wikipedia.org/wiki/LEB128)를 참고 하시면 되겠습니다.

자, 우선 가장 일반적인 시리얼라이징 예를 살펴 보겠습니다.

```csharp
void WriteString(Writer writer, string value)
{
    // UTF8로 변환
    var bytes = Encoding.UTF8.GetBytes(value);

    // 변환된 UTF8의 바이트 단위 길이를 varint 형태로 저장
    //   1바이트 : len <  (1 << 7)
    //   2바이트 : len <  (1 << 14)
    //   3바이트 : len <  (1 << 21)
    //   4바이트 : len <  (1 << 28)
    //   5바이트 : len >= (1 << 28)
    writer.WriteCounter(output, bytes.Length);

    // 변환된 UTF8 바이트 배열 기록
    if (bytes.Length > 0)
        writer.WriteRawBytes(bytes);
}
```

매우 일반적인 코드입니다. 무엇이 문제일까요?

여기서도 여전히 `UCS2 -> UTF8` 변환시에 임시 메모리 할당이 요구된다는 것입니다. `Encoding.UTF8.GetBytes()` 이함수 때문입니다.

자, 어떻게 이 할당을 제거할 수 있을까요?

임시 변환 버퍼 없이 타겟이 되는 버퍼에 직접 기록할 수 있다면, 좋을텐데 말입니다.
이글의 주제는 극단의 시리얼라이저를 만드는 과정에서 얻은 팁을 설명하는것이므로, 한번 극단적으로 최적화를 해보려합니다.

다음의 과정을 통해서 최적화를 수행해보도록 하겠습니다.

1. 문자열이 변환되었을때 차지할 최대로 필요한 버퍼의 길이만큼 미리 버퍼를 할당해줍니다.
2. 변환 타겟 버퍼를 할당된 버퍼로 지정합니다.
3. 실제 변환된 바이트 배열 길이만큼 commit합니다.

우선 문자열이 UTF8로 변환 되었을때 안전한 최대 길이를 계산해야합니다. C# 런타임은 아래와 같은 함수를 제공합니다.

```csharp
public override int UTF8Encoding.GetMaxByteCount(int charCount);
```
https://docs.microsoft.com/ko-kr/dotnet/api/system.text.utf8encoding.getmaxbytecount?view=netcore-3.1

이 함수의 기능은 지정한 수의 유니코드 문자를 인코딩할 경우 출력되는 최대 바이트 수를 계산해냅니다. 즉, 출력에 필요한 버퍼의 길이를 계산할 수 있는 것입니다.

우선 코드를 살펴 보겠습니다.

```csharp
public static unsafe void Write(this IMessageOut writer, string value)
{
    if (string.IsNullOrEmpty(value) == null)
    {
        writer.WriteCounter32(0);
        return;
    }

    // Begin
    // bufferLength = 필요한 최대 출력 버퍼 길이
    // predictedBodyOffset = 예측한 변환된 문자열 데이터 오프셋
    int position = WriteStringBegin(writer, value.Length, out int bufferLength, out int predictedBodyOffset);

    fixed (char* valuePtr = value)
    fixed (byte* bufferPtr = writer.Data)
    {
        // Zero copy conversion
        int byteCount = Encoding.UTF8.GetBytes(valuePtr, value.Length, bufferPtr + position + predictedBodyOffset, bufferLength);

        // End
        WriteStringEnd(writer, bufferPtr + position, predictedBodyOffset, byteCount);
    }
}
```

위 코드를 슬며시 살펴보면, 변환&쓰기 시작하는 부분과 변환&쓰기를 완료하는 두부분으로 나누어서 처리하고 있습니다.

```csharp
static int WriteStringBegin(IMessageOut writer, int characterLength, out int bufferLength, out int predictedBodyOffset)
{
    // 변환된 UTF8 바이트 데이터들을 출력하는데 필요한 최대 바이트 길이를 계산합니다.
    // (길이(varint:1~5 바이트)  +  인코등된 데이터)
    bufferLength = 5 + Encoding.UTF8.GetMaxByteCount(characterLength);

    // 필요한 최대 길이만큼 메모리 버퍼를 확보합니다.
    // (아직 실제로 write된것은 아니고 단순히 메모리를 확보(reserve) 하는 것입니다.)
    int position = writer.EnsureCapacity(bufferLength);

    // 우선은 변환전의 유니코드 문자열의 길이로 예상되는 UTF8 길이를 담을 변수의 바이트 길이를 계산합니다.
    // 변환 후 예상했던 길이와 다를 수 있습니다. 이 부분에 대해서는
    // WriteStringEnd 함수에서 다룹니다.

    if (characterLength < 1u << 7) // 128 보다 작으면 길이 변수는 1바이트
        predictedBodyOffset = 1;
    else if (characterLength < 1u << 14) 
        predictedBodyOffset = 2;
    else if (characterLength < 1u << 21)
        predictedBodyOffset = 3;
    else if (characterLength < 1u << 28)
        predictedBodyOffset = 4;
    else
        predictedBodyOffset = 5;

    return position;
}
```

위 함수는 UTF로 변환된 문자열 데이터가 차지할 메모리양을 예측하고, 문자열의 길이가 차지하는 바이트 길이를 예측합니다. 왜 계산이 아니라 예측한다고 말했을까요? 말 그대로 정확한 수치가 아니라는 것입니다. 실제 변환후에 결과를 살펴봐야 비로서 정확한 갓을 알아낼 수 있다는 얘기입니다.

변환 데이터를 담을 버퍼의 길이는 절대 부족해서는 안됩니다. 한번에 변환 결과를 담을 수 있도록 충분히 잡아주어야합니다.

또한 두번째로 변환된 바이트 데이터의 길이를 담을 변수의 길이는 아직 변환 전이므로 `유니코드 문자`의 갯수를 기준으로 계산된것이므로, 변환 후에 대조해보면 늘어날수도 있고 아닐 수도 있기 때문에 `WriteStringEnd` 함수 이후에 정확한 값으로 다시 적용해주어야 합니다.

```csharp
private static unsafe void WriteStringEnd(IMessageOut output, byte* bufferPtr, int predictedBodyOffset, int encodedByteLength)
{
    if (encodedByteLength < 1u << 7)
    {
        if (predictedBodyOffset != 1)
            Memmove(bufferPtr + predictedBodyOffset, bufferPtr + 1, encodedByteLength, encodedByteLength);

        // Length of UTF8 encoded string body
        bufferPtr[0] = (byte)encodedByteLength;

        // 실제로 기록한 데이터 길이를 적용해줍니다.
        output.Advance(encodedByteLength + 1);
    }
    else if (encodedByteLength < 1u << 14)
    {
        if (predictedBodyOffset != 2)
            Memmove(bufferPtr + predictedBodyOffset, bufferPtr + 2, encodedByteLength, encodedByteLength);

        // Length of UTF8 encoded string body
        bufferPtr[0] = (byte)(encodedByteLength | 0x80);
        bufferPtr[1] = (byte)((encodedByteLength >> 7));

        // 실제로 기록한 데이터 길이를 적용해줍니다.
        output.Advance(encodedByteLength + 2);
    }
    else if (encodedByteLength < 1u << 21)
    {
        if (predictedBodyOffset != 3)
            Memmove(bufferPtr + predictedBodyOffset, bufferPtr + 3, encodedByteLength, encodedByteLength);

        // Length of UTF8 encoded string body
        bufferPtr[0] = (byte)(encodedByteLength | 0x80);
        bufferPtr[1] = (byte)((encodedByteLength >> 7) | 0x80);
        bufferPtr[2] = (byte)((encodedByteLength >> 14));

        // 실제로 기록한 데이터 길이를 적용해줍니다.
        output.Advance(encodedByteLength + 3);
    }
    else if (encodedByteLength < 1u << 28)
    {
        if (predictedBodyOffset != 4)
            Memmove(bufferPtr + predictedBodyOffset, bufferPtr + 4, encodedByteLength, encodedByteLength);

        // Length of UTF8 encoded string body
        bufferPtr[0] = (byte)(encodedByteLength | 0x80);
        bufferPtr[1] = (byte)((encodedByteLength >> 7) | 0x80);
        bufferPtr[2] = (byte)((encodedByteLength >> 14) | 0x80);
        bufferPtr[3] = (byte)((encodedByteLength >> 21));

        // 실제로 기록한 데이터 길이를 적용해줍니다.
        output.Advance(encodedByteLength + 4);
    }
    else
    {
        if (predictedBodyOffset != 5)
            Memmove(bufferPtr + predictedBodyOffset, bufferPtr + 5, encodedByteLength, encodedByteLength);

        // Length of UTF8 encoded string body
        bufferPtr[0] = (byte)(encodedByteLength | 0x80);
        bufferPtr[1] = (byte)((encodedByteLength >> 7) | 0x80);
        bufferPtr[2] = (byte)((encodedByteLength >> 14) | 0x80);
        bufferPtr[3] = (byte)((encodedByteLength >> 21) | 0x80);
        bufferPtr[4] = (byte)((encodedByteLength >> 28));

        // 실제로 기록한 데이터 길이를 적용해줍니다.
        output.Advance(encodedByteLength + 5);
    }
}
```

위 함수는 `WriteStringBegin` 함수와는 다르게 `unsafe` 코드가 사용되었습니다. 함수 내에서 포인터를 직접 다루기 위함입니다.
