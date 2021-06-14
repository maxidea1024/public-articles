namespace G.Network
{
    // Queue안의 element 값을 수정하기 위해서 사용됨.
    // 단 이렇게 하면, element 마다 할당이 하나 추가되는 부작용이 있음.
    internal class ElementWrapper<T>
    {
        public T Value { get; set; }
    }
}
