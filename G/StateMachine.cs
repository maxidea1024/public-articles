using System;
using System.Collections.Generic;

namespace Prom.Core.Util
{
    /// <summary>
    /// Simple State Machine
    /// </summary>
    public class StateMachine<T> where T : struct, IConvertible, IComparable
    {
        public int TurnCount => _stateTurnCount;

        class StateItem
        {
            public Action OnEnter { get; set; }
            public Action OnExit { get; set; }
            public Action OnUpdate { get; set; }
        }

        private readonly Dictionary<T, StateItem> _states = new Dictionary<T, StateItem>();
        private T _state = default;
        private T _nextState = default;
        private StateItem _stateItem = null;
        private long _lastStateChangedTime;
        private int _stateTurnCount = 0;

        public T State
        {
            get => _state;

            set
            {
                _nextState = value;
                SolvePendingTransition();
            }
        }

        public T NextState
        {
            get => _nextState;

            set
            {
                if (!_nextState.Equals(value))
                {
                    _nextState = value;
                }
            }
        }

        public long ElapsedTime => SystemClock.Milliseconds - _lastStateChangedTime;

        public StateMachine()
        {
            _lastStateChangedTime = SystemClock.Milliseconds;
            _stateTurnCount = 0;
        }

        public StateMachine(T defaultState/*, bool invokeDefaultStateOnEnterCallback = false*/) : base()
        {
            // 최초 선택되는 상태의 OnEnter 콜백이 호출되게 되면 문제가 생길 수 있을텐데..
            _state = defaultState;
            _nextState = defaultState;
        }

        public void RegisterState(T state, Action onEnter, Action onExit, Action onUpdate)
        {
            //todo 중복체크. 그냥 업데이트 하면 될듯?
            _states.Add(state, new StateItem
            {
                OnEnter = onEnter,
                OnExit = onExit,
                OnUpdate = onUpdate
            });
        }

        public void Update()
        {
            SolvePendingTransition();

            if (_stateItem != null)
            {
                var onUpdate = _stateItem.OnUpdate;
                onUpdate?.Invoke();

                _stateTurnCount++;
            }
        }

        private StateItem GetStateItem(T state)
        {
            _states.TryGetValue(state, out StateItem stateItem);
            return stateItem;
        }

        private void SolvePendingTransition()
        {
            if (_state.Equals(_nextState))
            {
                return;
            }

            if (_stateItem != null)
            {
                var onExit = _stateItem.OnExit;
                onExit?.Invoke();
            }

            _lastStateChangedTime = SystemClock.Milliseconds;
            _stateTurnCount = 0;

            _state = _nextState;
            _stateItem = GetStateItem(_state);

            if (_stateItem != null)
            {
                var onEnter = _stateItem.OnEnter;
                onEnter?.Invoke();
            }
        }
    }
}
