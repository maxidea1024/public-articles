## PROM Grammar

간단한 인터페이스 정의 예는 아래와 같습니다.

```csharp
namespace dotnet "Greeting";

sservice Greeting {
  SayHello(HelloRequest)
      returns (HelloResponse);
}

struct HelloRequest {
  name :string;
}

struct HelloResponse {
  greeting :string;
}
```

<br>

### Module

`Module`은 `.prom` 파일 자체를 `Module`이라고 칭합니다. `Module` 파일 안에는 각종 정의들이 담기게 됩니다. 또한, 다른 `.prom` 파일 즉, `Module`을 포함할 수 있습니다.

```csharp
namespace dotnet "Example";

import "Types";
import "Errors";
import "Models";

  .
  .
  .
```

### Struct

`struct`는 `protobuf`의 `message`와 같은 같은 성격을 가지는 타입입니다. `struct`는 여러개의 필드들로 구성될 수 있으며, 상속은 지원하지 않습니다. 각 필드 앞에는 `protobuf`와 같이 태그를 지정할 수 있으며, 생략도 가능합니다. 단, 생략했을 경우에는 중간에 필드아이디를 지정하거나 버젼업시에 필드 아이디 지정 없이 중간에 필드를 끼워넣으면 호환이 되지 않는 등의 주의 사항이 있습니다.

```csharp
namespace dotnet "Example";

// Without field tags
struct Monster {
  name :string;
  level :int;
}

// With field tags
struct Item {
  @1 index :int64;
  @2 count :int32;
}
```

### Oneof

`C언어`의 공용체(union)과 같은 속성을 가지고 있는 타입니다. `oneof`내에 정의된 필드들중 하나만 유효한 값을 가지며, 어떤값이 유효한지는 생성된 코드에서 `which` 접근자로 처리할 수 있습니다. `protobuf`의 `oneof`와 유사합니다. `protobuf`가 `message`내에 정의하는것과는 다르게 `struct`처럼 독립적으로 정의해야합니다.

```csharp
namespace dotnet "Example";

oneof SizeUnit  {
  pound :int32;
  kilograms :int64;
  standardCrates :int16;
  metricTons :double;
}
```

### Exception

서비스 함수 구현코드에서 발생할 수 있는 예외 타입들입니다. `API` 사용 문서 작성시에 해당 `API`에서 발생하는 서비스 오류들을 전달해야하는데, 별도의 문서에 기입하는 방법보다는 `.prom` 파일내에 정의해두면 자동으로 `API` 사용관련 문서가 생성될때 발생 가능한 예외 또는 오류코드가 출력되므로, 별도의 `API` 도움말을 작성할 필요가 없습니다.

기본적으로 코드 생성기는 예외를 사용하는 코드를 생성합니다. 자체 방침등에 따라서 예외 사용이 허용되는 않는 환경에서는 코드 생성시에 `--no-exceptions-handling` 옵션을 주어서 예외 관련 코드가 생성되지 않도록 해야합니다. 이때는 예외 대신 오류코드를 사용하게 되는데, 예외 정의시에 반듯이 예외정의 시작 부분에 오류코드를 명시해야합니다.

```csharp
namespace dotnet "Example";

exception NotFoundException {
  reason :string;
}

// With error codes
exception(404)  NotFoundException {
  reason :string;
}
```

```csharp
sservice Auth {
  /** 로그인을 요청합니다. */
  Login(LoginRequest)
      returns (LoginResponse)
      throws (
          /** 계정이 블럭 되었습니다. */
          AccountBlockedException,
          /** 점검중입니다. */
          InMaintenanceException,
          /** 잘못된 요청입니다. */
          InvalidRequestException,
      );
}

/** 해당 계정이 블럭되었을 경우에 발생 */
exception AccountBlockedException {
}

/** 서버가 점검중일때 발생 */
exception InMaintenanceException {
}

/** 잘못된 요청이 왔을때 발생 */
exception InvalidRequestException {
}
```

### Service

여타 `IDL`과는 다르게 3가지의 서비스를 지원합니다. 다소 의아할수도 있는데, 제가 생각했던 모든 목적에 맞는 일반환된 서비스 형태를 구현하기 쉽지 않았고, 그결과 3가지의 서비스로 분화하기로 결정했습니다. 구체적으로 다음과 같은 서비스들을 지원합니다.

- Simple Service
- Regular Service
- Realtime Service

상세한 내용은 아래의 내용을 참조하시길 바랍니다.


#### Simple Service

`gRPC`등에서 지원하는 단순한 `Request`, `Response` 형태를 지원하는 서비스입니다. `Request`, `Response` 에 허용되는 타입은 `struct`, `void` 두 타입뿐입니다. 즉, 요청/응답이 있거나 없거나만 지정할 수 있습니다.

```csharp
namespace dotnet "Example";

sservice Greeting {
  SayHello(HelloRequest) returns (HelloReply);

  SayBye();
}

struct HelloRequest {
  name :string;
}

struct HelloReply {
  greeting :string;
}
```

#### Regular Service

본질적으로 위에서 얘기한 `Simple Service`와 동일합니다. 다만, `Request`, `Response`에 각각 한개의 `struct`가 와야하는 `Simple Service`와는 다르게 일반적인 `C언어` 함수정의 형태와 유사한 형태를 가집니다. `Simple Service`를 사용하지 않고, `Regular Service`를 사용하는 장점은 무엇일까요? `Simple Service`는 `Request`, `Response` `struct`를 정의해야하는 부담이 있는데 반해, `Regular Service`는 그런 부담이 없습니다. 그외에는 차이가 없으며, 실제 내부 구현도 동일합니다. 단지, `Request`, `Response` 구조체 정의를 안해도 된다만 다릅니다.

```csharp
namespace dotnet "Example";

rservice Greeting {
  SayHello(name :string) returns (string);
}

// or

rservice Greeting {
  SayHello(request :HelloRequest) returns (HelloReply);
}

struct HelloRequest {
  name :string;
}

struct HelloReply {
  greeting :string;
}
```

#### Realtime Service

위의 두개 서비스 형태와는 완전히 다른 종류의 서비스입니다. 위 두 서비스와의 차이점은 아래와 같습니다.

- 반환 값을 가질 수 없습니다.
- 예외를 처리하지 않습니다.
- 서버측의 응답을 기다리지 않습니다.

예외처리도 없고 반환값 처리도 없는 이런 서비스를 어디에 사용해야할지 의문이 들것입니다. `실시간` 게임 네트워킹의 경우 `Request`, `Resonse` 형태가 아닌 경우가 많으므로, 이때 사용하기 위해 사용하는 서비스입니다. 예를들어, 플레이어가 서버에게 움직임을 요청했을때 움직임 요청에 대한 결과가 아닌, 상호작용이 일어나는 일련의 상태를 클라이언트에게 보내주는 형태들에 사용하기 위한 서비스입니다. 또한, `Client to Server`, `Server to Client` 외에 `Peer to Peer` 통신에 적용하기도 용이합니다.

`Realtime Service`는 게임만을 위한 전용 기능이기에 웹서비스에 적합하지 않습니다.

```csharp
namespace dotnet "Example";

rtservice GameC2S {
  /** 서버에 이동하기를 요청합니다. */
  Move(
        /** 이동할 x 좌표입니다. */
        x :int,
        /** 이동할 y 좌표입니다. */
        y :int
      );
}

rtservice GameS2C {
  /** 서버에서 클라이언트로 이동 관련한 이벤트를 전송합니다. */
  MoveSync(moveEvents :MoveEvent[]);
}
```

### 열거형

`C 언어`의 그것처럼 나열식을 정의할 수 있습니다. 뒤에 값을 지정하지 않으면 숫자 `0`부터 차례로 증가한 값이 설정됩니다. 명확하게 하고 싶다면, 뒤에 숫자를 지정해줍니다.

```csharp
enum AnimalType {
  Elephant,
  Tiger,
  Lion,
}
```

### 상수

기존 언어들의 상수정의와 유사하며, `.prom` 파일내에서 사용이 가능하며 동시에 코드 생성시에 해당 정의와 값들이 출력되어서 프로그래머가 바로 사용할 수 있습니다. 이를 활용하면 하나의 파일에 공통된 정의들이 담기게 되므로 관리가 용이해지는 효과가 있습니다.

```csharp
namespace . "Example";

const MAX_PATHLEN :int = 512;
const DEFAULT_KEY :string = "blabla";
```

### Import

하나의 `.prom` 파일에 모든 정의를 넣을 수도 있지만, 내용이 너무 많아지면 읽기도 불편하고, 관리의 부담이 늘어나게 됩니다. 이때 적당히 분류해서 여러개의 `.prom` 파일로 나누어서 관리하게 되면, 작업이 수월해질것입니다. 이때 사용하는 구문이 `import` 구문입니다. 다른 `.prom` 파일을 임포트 하게 되면 바로 접근이 불가능하고 `파일명.이름` 형태로 접근할 수 있습니다. 이게 불편하다면 전역 스코프에 맵핑할 수 있습니다. 이때는 `import . 파일명` 형태로 지정해면 됩니다.

```csharp
// ErrorCodes.prom

enum ErrorCode {
  .
  .
  .
}
```

```csharp
// Types.prom

```

```csharp
// All.prom

import "ErrorCodes.prom"
import "Types.prom"
```

### Namespace

`Namespace`는 `IDL`내에서는 아무런 기능도 하지 않는 요소입니다. `Namespace`는 코드 생성시에 지정한 언어에 적용될 `namespace`를 설정할 수 있도록 해줍니다.

`C#` 코드의 예를 보자면,

```csharp
// MyGame.prom
namespace dotnet MyGame;

struct AvatarInfo {
  .
  .
  .
}
```

위 정의 기준으로 생성된 C# 코드입니다.

```csharp
// MyGame.cs
namespace MyGame
{
    public class AvatarInfo : global::Prom.Core.IGeneratedStruct
    {
        .
        .
        .
    }
}
```

### Typedef

특정한 타입을 좀더 의미있게 사용하고 싶을 때가 있습니다. 예를들어서 `UserID`에 `int` 타입을 사용할 경우 `UserID`를 정의해서 사용하고 싶을 것입니다. 그럴때 사용하는 것이 `Typedef`입니다. 다만, 생성 타겟 언어에서 `typedef`를 지원하지 않을 경우, 본래의 타입으로 출력 됩니다.

```csharp
typedef UserID :int;

struct UserInfo {
  userId :UserID;
}
```

### Containers

`PROM`에서는 세가지의 컨테이너를 타입을 지원합니다.

  - Map
  - Set
  - List

이 타입들은 역직렬화(deserailization)시에 `Lazy` 로드 됩니다. 이 말은 접근하는 순간에 실제로 데이터를 읽어서 컨테이너 객체를 생성한다는 것입니다. 이러한 처리가 필요한 이유는 컴테이너는 대부분 크기가 클수가 있습니다. 이때 아직 사용하지도 않은 요소들을 위해서 무조건 읽어들이고 객체를 생성하는 것보다는 필요할때(접근할때) 읽어들이고 생성하는게 효율적일 수 있기 때문입니다. 단, 이러한 처리를 위해서 부가 정보가 필요하게 되고 이를 네트워크 넘어로 전송해야 하므로 야간의 크기는 증가될 수 있습니다. 해당 기능은 옵션형태로 활성화/비활성화 할 수 있습니다.

### Annotations

일종의 `Meta` 정보로서 코드 생성시에 옵션 처리등을 할 수 있는 근거가 될 수 있습니다.

### 문서화

문서화는 `API` 정의시에 매우 중요한 부분입니다. 대부분의 경우 작성자와 사용자가 다르기 때문입니다. 통상적으로 별도의 `API` 사용법을 담은 문서작업을 하게 됩니다. 이 작업은 적지 않은 부담이며, 개발자들에게는 때로는 고통일 수 있습니다. `PROM`에서는 `thrift` 처럼 `javadoc` 스타일의 주석을 달아주면 자동으로 문서화게 되도록 설계되어 있습니다. 이를 잘 활용하면, 별도의 `API` 문서작업이 필요하지 않을 수 있습니다.

```csharp
/** The greeter sevice definition. */
sservice Greeter {
  /** Sends a greeting */
  SayHello(HelloRequest) return (HelloReply);
}

/** The request message containing the user's name. */
struct HelloRequest {
  /** The user's name */
  name :string;
}

/** The response message containing the greetings. */
struct HelloReply {
  /** The greeting message. */
  message :string;
}
```


---


전체 문법
---

### MODULE

```
[] MODULE ::= HEADER* DEFINITION*
```

### HEADER

```
[] HEADER ::= IMPORT | NAMESPACE
```

### IMPORT

```
[] IMPORT ::= 'import' IMPORT_SCOPE? LITERAL

[] IMPORT_SCOPE ::= '.'
```

### NAMESPACE

```
[] NAMESPACE ::= 'namespace' NAMESPACE_LANG IDENTIFIER SEPARATOR?

[] NAMESPACE_LANG ::= '*' | 'cpp' | 'dotnet' | 'py' | 'go'
```

### DEFINITION

```
[] DEFINITION ::= CONST | TYPEDEF | ENUM | STRUCT | ONEOF | EXCEPTION | SERVICES

[] SERVICES ::= REGULAR_SERVICE | SIMPLE_SERVICE | REALTIME_SERVICE
```

### CONST

```
[] CONST ::= 'const' IDENTIFIER ':' FIELD_TYPE '=' CONST_VALUE SEPARATOR?
```

### TYPEDEF

```
[] TYPEDEF ::= 'typedef' IDENTIFIFER ':' DEFINITION_TYPE SEPARATOR?
```

### ENUM

```
[] ENUM ::= 'enum' IDENTIFIER '{' (IDENTIFIER ('=' INT_CONSTANT)? SEPARATOR?)* '}'
```

### STRUCT

```
[] STRUCT ::= 'struct' IDENTIFIER '{' FIELD* '}'
```

### ONEOF

```
[] ONEOF ::= 'oneof' IDENTIFIER '{' FIELD* '}'
```

### EXCEPTION

```
[] EXCEPTION ::= 'exception' EXCEPTION_ID? IDENTIFIER '{' FIELD* '}'

[] EXCEPTION_ID ::= '(' INT_CONSTANT ')'
```

### SIMPLE SERVICE

```
[] SIMPLE_SERVICE ::= 'sservice' IDENTIFIER ('extends' IDENTIFIER)? '{' SIMPLE_FUNCTION* '}'
```

### REGULAR SERVICE

```
[] REGULAR_SERVICE ::= 'rservice' IDENTIFIER ('extends' IDENTIFIER)? '{' REGULAR_FUNCTION* '}'
```

### REALTIME SERVICE

```
[] REALTIME_SERVICE ::= 'rtservice' IDENTIFIER ('extends' IDENTIFIER)? '{' SIMPLE_FUNCTION* '}'
```

### SIMPLE FUNCTION

```
[] SIMPLE_FUNCTION ::= FUNCTION_ID? 'oneway'? IDENTIFIER '(' SIMPLE_REQUEST? ')' SIMPLE_RESPONSE? THROWS? SEPARATOR?

[] SIMPLE_REQUEST ::= IDENTIFIER | 'void'

[] SIMPLE_RETURNS ::= 'returns' '(' SIMPLE_RESPONSE ')'

[] SIMPLE_RESPONSE ::= IDENTIFIER | 'void'
```

### REGULAR FUNCTION

```
[] REGULAR_FUNCTION ::= FUNCTION_ID? 'oneway'? IDENTIFIER '(' FIELD* ')' REGULAR_RETURNS? THROWS? SEPARATOR?

[] REGULAR_RETURNS ::= 'returns' '(' FIELD_TYPE ')'
```

### REALTIME FUNCTION

```
[] REALTIME_FUNCTION ::= FUNCTION_ID? IDENTIFIER '(' FIELD* ')' SEPARATOR?

```

### FIELD

```
[] FIELD ::= FIELD_ID? FIELD_SPEC? IDENTITIFER ':' FIELD_TYPE ('=' CONST_VALUE)? SEPARATOR?
```

### FIELD ID

```
[] FIELD_ID ::= '@' INT_CONSTANT
```

### FIELD SPECIFIER

```
[] FIELD_SPEC ::= 'required' | 'optional' | 'deprecated'
```

### THROWS

```
[] THROWS ::= 'throws' '(' THROW_FIELD* ')'

[] THROW_FIELD ::= FIELD_ID? IDENTIFIER SEPARATOR?
```

### FUNCTION ID

```
[] FUNCTION_ID ::= '@' INT_CONSTANT
```

### TYPES

```
[] FIELD_TYPE ::= IDENTIFIER | PRIMITIVE_TYPE | CONTAINER_TYPE

[] DEFINITION_TYPE ::= PRIMITIVE_TYPE | CONTAINER_TYPE

[] PRIMITIVE_TYPE ::= 'bool'
              | 'int8' | 'int16' | 'int32' | 'int64' | 'uint8' | 'uint16' | 'uint32' | 'uint64'
              | 'fixed8'| 'fixed16'| 'fixed32'| 'fixed64' | 'sfixed8'| 'sfixed16'| 'sfixed32'| 'sfixed64'
              | 'bool' | 'float' | 'double' | 'float32' | 'float64'
              | 'string' | 'bytes'
              | 'datetime' | 'datetimelite' | 'timespan'
              | 'uuid'
              | 'int' | 'uint' | 'long' | 'ulong'
              | 'byte' | 'word' | 'dword' | 'qword' | 'sbyte' | 'sword' | 'sdword' | 'sqword'


[] CONTAINER_TYPE ::= MAP | SET | LIST

[] MAP ::= 'map' '<' FIELD_TYPE '>'

[] SET ::= 'set' '<' FIELD_TYPE '>'

[] LIST ::= ('list' '<' FIELD_TYPE '>') | (FIELD_TYPE '[' INT_CONSTANT? ']')
```

### CONSTANT VALUES

```
[] CONST_VALUE ::= INT_CONSTANT | DOUBLE_CONSTANT | LITERAL | IDENTIFIER | CONST_MAP

[] INT_CONSTANT ::= INT_CONSTANT1 | INT_CONSTANT2

[] INT_CONSTANT1 ::= ('+' | '-')? DIGIT+

[] INT_CONSTANT2 ::= ('+' | '-')? DIGIT ('_' | DIGIT)*

[] DOUBLE_CONSTANT ::= ('+' | '-')? DIGIT* ('.' DIGIT+)? ( ('E' | 'e') INT_CONSTANT )?

[] CONST_LIST ::= '[' (CONST_VALUE SEPARATOR?)* ']'

[] CONST_MAP ::= '{' (CONST_VALUE ':' CONST_VALUE SEPARATOR?)* '}'
```

### ANNOTATIONS

```
[] ANNOTATIONS ::= '(' ANNOTATION* ')'

[] ANNOTATION ::= IDENTIFIER | (IDENTIFIER ':' (IDENTIFIER | LITERAL | INT_CONSTANT | DOUBLE_CONSTANT))
```

### BASIC DEFINITIONS

#### LITERAL
```
[] LITERAL ::= SINGLE_QUOTED_LITERAL | VERBATIM_LITERAL

[] SINGLE_QUOTED_LITERAL ::= ('"' [^"]* '"') | ("'" [^']* "'")

[] VERBATIM_LITERAL ::= ('"""' [.]* '"""') | ("'''" [.]* "'''")
```

#### IDENTIFIER
```
[] IDENTIFIER ::= ( LETTER | '_' ) ( LETTER | DIGIT | '.' | '_' )*
```

#### SEPARATOR
```
[] SEPARATOR ::= ',' | ';'

[] LIST_SEPARATOR ::= ','
```

#### LETTERS AND DIGITS
```
[] LETTER ::= ['A'-'Z'] | ['a'-'z']

[] DIGIT ::= ['0'-'9']
```
