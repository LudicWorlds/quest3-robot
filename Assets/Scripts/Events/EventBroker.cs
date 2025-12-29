using UnityEngine;
using System.Collections.Generic;
using System;

// Reference: http://www.codeproject.com/Articles/30066/EventBroker-a-notification-component-for-synchrono


public class EventBroker
{
    private static EventBroker _instance;

    //------------------------------
    // Properties
    //------------------------------
    private Dictionary<string, EventHandler<EventArgs>> _events;
    public Dictionary<string, EventHandler<EventArgs>> Events
    {
        get { return _events; }
    }


    //--------------------------------
    // Methods
    //--------------------------------

    public static EventBroker GetInstance()
    {
        if (_instance == null)
        {
            _instance = new EventBroker();
        }

        return _instance;
    }


    private EventBroker()
    {
        //Debug.Log("-> Private Event Manager");
        this._events = new Dictionary<string, EventHandler<EventArgs>>();



        // _eventBroker.CreateEventHandler(EventID.TICKER_PANEL_CREATED);
        // _eventBroker.CreateEventHandler(EventID.CRYPTO_DATA_RECEIVED);
    }


    public void CreateEventHandler(string id)
    {
        this._events.Add(id, null);
    }


    public void DestroyEventHandler(string id)
    {
        if (this._events.ContainsKey(id))
        {
            this._events[id] = null;
            this._events.Remove(id);
        }
        else
        {
            Debug.LogError("-> EventBroker::DestroyEventHandler - can't find Event with Id(Key): " + id);
        }
    }


    public void DispatchEvent(string id)
    {
        DispatchEvent(id, EventArgs.Empty);
    }


    public void DispatchEvent(string id, EventArgs eventArgs)
    {
        if (this._events[id] != null)
        {
            lock (this._events)
            {
                this._events[id](this, eventArgs);
            }
        }
    }


    public void ClearAllEvents()
    {
        _events.Clear();
    }


    public void Dispose()
    {
        ClearAllEvents();
        _instance = null;
    }
}

