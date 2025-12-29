using System;

namespace LudicWorlds
{
	public abstract class State<T> : IState<T>
	{
        protected T _id;
		protected IStateMachine<T> _stateMachine;

        protected Boolean _isDisposed = false;


		public T ID 
		{
			get { return _id; }
		}
		
		public State(IStateMachine<T> stateMachine, T id)
		{
			this._id = id;
			this._stateMachine = stateMachine;
		}

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();

        public virtual void Dispose()
        {
            _id = default(T);
            _stateMachine = null;
            _isDisposed = true;
        }      
	}
}


