using UnityEngine;
using System.Collections.Generic;

namespace LudicWorlds
{
    public class GameObjectStateMachine<T> : MonoBehaviour, IStateMachine<T>
    {
        protected GameObjectState<T> _currentState;
        protected GameObjectState<T> _prevState;
        protected GameObjectState<T> _nextState;
        protected Dictionary<T, GameObjectState<T>> _states;

        //-------------------------------------------------------------------
        // Acessors
        //-------------------------------------------------------------------

        public IState<T> CurrentState
        {
            get { return _currentState; }
        }

        public IState<T> PreviousState
        {
            get{ return _prevState; }
        }

        public IState<T> NextState
        {
            get { return _nextState; }
        }


        //-------------------------------------------
        // Monobehaviour
        //-------------------------------------------

        protected virtual void Awake()
        {
            //Debug.Log ("-> GameObjectStateMachine::Awake");
            InitStateMachine();
        }

        protected virtual void Start()
        {
            //Debug.Log("-> GameObjectStateMachine::Start");
            InitStates();
        }

        protected virtual void Update()
        {
            _currentState?.Update();
        }

        protected virtual void FixedUpdate() {}

        protected virtual void LateUpdate()
        {
            _currentState?.LateUpdate();
        }

        //-------------------------------------------
        // StateMachine 
        //-------------------------------------------

        private void InitStateMachine()
        {
            _currentState = null;
            _states = new Dictionary<T, GameObjectState<T>>();
        }

        protected virtual void InitStates() { }


        public void AddState(IState<T> state)
        {
            this._states.Add(state.ID, state as GameObjectState<T>);
        }


        public bool SetState(IState<T> state)
        {
            if (state != null)
            {
                _nextState = state as GameObjectState<T>;

                _currentState?.Exit();

                _prevState = _currentState;
                _currentState = state as GameObjectState<T>;
                _currentState.Enter();
                return true;
            }
            else
            {
                return false;
            }
        }


        public bool SetState(T stateID)
        {
            if (_currentState != null)
            {
                if (stateID.Equals(_currentState.ID))
                    return true;
            }

            if(!_states.ContainsKey(stateID))
            {
                Debug.LogError("LL-> GameObjectStateMachine::ChangeState() - Can't find stateID: " + stateID);
                return false;
            }

            return SetState( _states[stateID] );
        }

        public void ClearStates()
        {
            //Dispose of resources within each state
            foreach (KeyValuePair<T, GameObjectState<T>> state in _states)
            {
                state.Value.Dispose();
            }

            _currentState = null;
            _prevState = null;
            _nextState = null;

            _states.Clear();
            _states = null;
        }

        protected virtual void OnDestroy()
        {
            ClearStates();
        }
    }
}

