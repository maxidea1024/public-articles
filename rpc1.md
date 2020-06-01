
```proto
message Request {

    // 기본 헤더

    optional uint32 protocol_version = 1;
    optional uint32 protocol_id = 2;
    optional uint32 seq_no = 3;
    optional string session_token = 4;


    // 프로토콜 추가

    optional RegisterReq register_req = 100;
    optional LoginReq login_req = 101;
    optional LogoutReq logout_req = 102;
    optional UserInfoReq user_info_req = 103;
    optional FindFiendReq friend_friend_req = 104;
    optional AddFriendReq add_friend_req = 105;
    optional DeleteFrienReq delete_friend_req = 106;
    optional JoinOrCreateRoomReq join_or_create_room_req = 107;
    optional JoinRoomReq join_room_req = 108;
    optional LeaveRoomReq leave_room_req = 109;
    optional RandomJoinRoomReq random_join_room_req = 110;


    // 이하 2~300개의 프로토콜 정의됨

        .
        .
        .

}
```


우연히 이런식으로 정의해서 사용하는 글들을 보게 되었는데, `gRPC` 사용하지 않고 `커스텀 rpc 시스템` 구현하느라 이렇게 사용했다고 하는데,
(참고로, 그들은 게임 서버 작업을 아주 오랫동안 해온 베테랑들이다.)

문제는 프로토콜이 수백개 이상 되면, `serialization` 시에 `optional` 처리에 필요한 수백번의 `if` 가 사용된다는걸 몰랐던걸까?
아니면 내가 모르는 최적화가 이루어지는건가? 지금까지 살펴봐온(직접 구현 포함) 구현체들은 `serialization 코드 생성시`에
`optional` 처리를 위해서 `최소 한번 이상의 if`가 사용된다.

(물론, `deserialization` 시에는 상관 없음)

```cpp
void Request::Serialize(IOutputStream& output) {
    if (is_set_.Field1) {
        WriteRawTag(8);
        SerializeField(field1_);
    }

    if (is_set_.Field2) {
        WriteRawTag(9);
        SerializeField(field2_);
    }

    if (is_set_.Field3) {
        WriteRawTag(10);
        SerializeField(field3_);
    }

    .
    .
    .
}
```


편의성을 위한 `trade-off`인가 라고 하기에는 좀...
(뭐 2~300개의 무의미한 if 쯤이야 하면.. ㅎㅎ)

의외로 이렇게 구현하는 경우를 왕왕 보게 된다.

뭐, 그래도 돈만 잘번다고 하니 딱히 할말은 없음...

그러고 보니 의외로 게임에 맞게 `Request and Response` 형태와 `단방향 Realtime` 형태를 같이 지원하는 RPC 시스템이 없는듯 싶다.

내가 하나 만들어서 공개해야겠다 싶다.
