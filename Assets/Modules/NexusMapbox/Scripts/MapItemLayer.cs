using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Unity.Map;
using UnityEngine;

namespace Nexus.Map
{
    /// <summary>
    /// Unused in this app.
    /// </summary>    

    [System.Serializable]
    public class MapItemLayer
    {
        public enum State
        {
            Off,
            FadingIn,
            On,
            FadingOut,
        }

        public string Name;

        [SerializeField]
        private float yPos;

        [SerializeField]
        private bool scaleWhenRepositioning;

        [SerializeField]
        private MapItem prefab;

        private float fadeDuration = 0.3f;

        private float timer;

        private List<MapItem> items = new List<MapItem>();

        private AnimationCurve curve;

        private Transform parent;

        private float alpha = 0f;

        public bool Visible => CurrentState == State.On || CurrentState == State.FadingIn;

        public State CurrentState { get; private set; }


        public void SetData<T>(IEnumerable<T> data)
        {
            UpdateMapLayer(data);
        }

        public void Show()
        {
            if (CurrentState == State.On || CurrentState == State.FadingIn)
            {
                return;
            }

            if (CurrentState == State.Off)
            {
                foreach (var item in items)
                {
                    item.gameObject.SetActive(item.HasData);
                    item.SetAlpha(0);
                }
            }

            CurrentState = State.FadingIn;
            curve = AnimationCurve.EaseInOut(fadeDuration, 0, 0, 1);

            timer = fadeDuration;
        }

        public void Hide(bool instant = false)
        {
            if (CurrentState == State.Off)
            {
                return;
            }

            if (instant)
            {
                foreach (var item in items)
                {
                    item.gameObject.SetActive(false);
                }

                CurrentState = State.Off;
            }
            else if (CurrentState == State.FadingOut)
            {
                return;
            }
            else
            {
                CurrentState = State.FadingOut;
                curve = AnimationCurve.EaseInOut(fadeDuration, 1, 0, 0);
                timer = fadeDuration;
            }
        }

        private void UpdateMapLayer<T>(IEnumerable<T> data)
        {
            if (parent == null)
            {
                parent = new GameObject(Name).transform;
            }

            int idx = 0;

            foreach (var d in data)
            {
                MapItem item;

                if (idx >= items.Count)
                {
                    item = GameObject.Instantiate<MapItem>(prefab, parent);
                    item.gameObject.SetActive(Visible);
                    item.SetAlpha(alpha);
                    items.Add(item);
                }
                else
                {
                    item = items[idx];
                    item.gameObject.SetActive(Visible);
                    item.SetAlpha(alpha);
                }

                item.Index = idx;
                item.Data = d;

                idx++;
            }

            for (int i = idx; i < items.Count; i++)
            {
                items[i].Recycle();
            }
        }

        public List<MapItem> GetItems()
        {
            return items;
        }

        public void Update(AbstractMap map)
        {
            if (CurrentState == State.Off)
            {
                return;
            }

            bool onThisFrame = false;

            if (CurrentState == State.FadingIn)
            {
                timer -= Time.deltaTime;

                alpha = curve.Evaluate(timer);

                if (timer <= 0)
                {
                    CurrentState = State.On;
                    onThisFrame = true;
                }
            }
            else if (CurrentState == State.FadingOut)
            {
                timer -= Time.deltaTime;

                alpha = curve.Evaluate(timer);

                if (timer <= 0)
                {
                    foreach (var item in items)
                    {
                        item.gameObject.SetActive(false);
                        item.SetAlpha(0);
                    }

                    CurrentState = State.Off;
                }
            }

            foreach (var item in items)
            {
                // reposition
                var pos = map.GeoToWorldPosition(item.LatLon, false);
                pos.y = yPos;
                item.transform.position = pos;

                // scale
                if (scaleWhenRepositioning)
                {
                    float scale = item.GetScale(map.transform.localScale.x);
                    item.transform.localScale = new Vector3(scale, scale, scale);
                }

                // fade
                if (CurrentState == State.FadingIn || CurrentState == State.FadingOut || onThisFrame)
                {
                    item.SetAlpha(alpha);
                }
            }
        }
    }
}
