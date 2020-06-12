## PROM

간단한 인터페이스 정의 예는 아래와 같습니다.

```csharp
import . "Exceptions.prom";

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

### Module 정의

`Module`은 `.prom` 파일 자체를 `Module`이라고 칭합니다. `Module` 파일안에는 각종 정의들이 담기게 됩니다. 또한, 다른 `.prom` 파일 즉, `Module`을 포함할 수 있습니다.

```csharp
namespace dotnet "Example";

import "Types";
import "Errors";
import "Models";

  .
  .
  .
```

### Struct 정의

`struct`는 `protobuf`의 `message`와 같은 같은 성질을 가지게 타입입니다. `struct`는 여러개의 필드들로 구성될 수 있으며, 상속은 지원하지 않습니다. 각 필드 앞에는 `protobuf`와 같이 태그를 지정할 수 있으며, 생략도 가능합니다. 단, 생략했을 경우에는 중간에 필드아이디를 지정하거나 버젼업시에 필드 아이디 지정 없이 중간에 필드를 끼워넣으면 호환이 되지 않는 등의 주의 사항이 있습니다.

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

### Oneof 정의

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

### Exception 정의

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

### Service 정의

여타 `IDL`과는 다르게 3가지의 서비스를 지원합니다. 다소 의아할수도 있는데, 제가 생각했던 모든 목적에 맞는 일반환된 서비스 형태를 구현하기 쉽지 않았고, 그결과 3가지의 서비스로 분화하기로 결정했습니다. 구체적으로 다음과 같은 서비스들을 지원합니다.

- Simple Service
- Regular Service
- Realtime Service

상세한 내용은 아래의 내용을 참조하시길 바랍니다.


#### Simple service 정의

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

#### Regular service 정의

본질적으로 위에서 얘기한 `Simple Service`와 동일합니다. 다만, `Request`, `Response`에 각각 한개의 `struct`가 와야하는 `Simple Service`와는 다르게 일반적인 `C언어` 함수정의 형태와 유사한 형태를 가집니다. `CORBA`, `thrift`의 함수정의와 같다고 생각하면 됩니다.

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

#### Realtime service 정의

위의 두개 서비스 형태와는 완전히 다른 종류의 서비스입니다. 위 두 서비스와의 차이점은 아래와 같습니다.

- 반환 값을 가질 수 없습니다.
- 예외를 처리하지 않습니다.
- 생성되는 인터페이스 코드들이 위의 두 서비스와는 완전히 다릅니다.

예외처리도 없고 반환값 처리도 없는 이런 서비스를 어디에 사용해야할지 의문이 들것입니다. `실시간` 게임 네트워킹의 경우 `Request`, `Resonse` 형태가 아닌 경우가 많으므로, 이때 사용하기 위해 사용하는 서비스입니다. 예를들어, 플레이어가 서버에게 움직임을 요청했을때 움직임 요청에 대한 결과가 아닌, 상호작용이 일어나는 일련의 상태를 클라이언트에게 보내주는 형태들에 사용하기 위한 서비스입니다.

```csharp
namespace dotnet "Example";

rtservice GameC2S {
  Move(actorId :int, x :int, y :int);
}

rtservice GameS2C {
  MoveSync(moveEvents :MoveEvent[]);
}
```

### 상수정의

기존 언어들의 상수정의와 유사하며, `.prom` 파일내에서 사용이 가능하며 동시에 코드 생성시에 해당 정의와 값들이 출력되어서 프로그래머가 바로 사용할 수 있습니다. 이를 활용하면 하나의 파일에 공통된 정의들이 담기게 되므로 관리가 용이해지는 효과가 있습니다.

```csharp
namespace . "Example";

const MAX_PATHLEN :int = 512;
const DEFAULT_KEY :string = "blabla";
```

### 문서화

문서화는 `API` 정의시에 매우 중요한 부분입니다. 대부분의 경우 작성자와 사용자가 다르기 때문입니다. 통상적으로 이부분을 상쇄하기 위해서, 별도의 `API` 사용법을 담은 문서작업을 하게 됩니다. 이 작업은 적지 않은 부담이며, 개발자들에게는 때로는 고통일 수 있습니다. `PROM`에서는 `thrift` 처럼 `javadoc` 스타일의 주석을 달아주면 자동으로 문서화게 되도록 설계되어 있습니다. 이를 잘 활용하면, 별도의 `API` 문서작업이 생략될수도 있습니다.

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
[] IMPORT ::= 'import' '.'? LITERAL
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
