using System;

namespace LudicWorlds
{
	public interface IStateMachine<T>
	{
		IState<T> CurrentState
		{
			get;
		}

        IState<T> PreviousState
        {
            get;
        }

        IState<T> NextState
        {
            get;
        }

        //void Update();
        void AddState( IState<T> state );
		bool SetState( IState<T> state );
		bool SetState(T stateID); //state could be an int, string, Enum...

        void ClearStates();
	}
}


