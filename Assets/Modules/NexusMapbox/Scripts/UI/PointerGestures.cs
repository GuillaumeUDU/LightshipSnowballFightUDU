using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Nexus.UI
{
    /// <summary>
    /// Various helper properties for managing user touch input
    /// </summary>
    public static class PointerGestures
    {
        static PointerGestures()
        {
            EnhancedTouchSupport.Enable();

            Application.focusChanged += Application_focusChanged;
        }

        private static void Application_focusChanged(bool newFocus)
        {
            if (newFocus)
            {
                if (!EnhancedTouchSupport.enabled)
                {
                    EnhancedTouchSupport.Enable();
                }
            }
            else
            {
                EnhancedTouchSupport.Disable();
            }
        }

        public static Vector2 PointerPos => Pointer.current.position.ReadValue();

        public static bool PointerDownThisFrame => Pointer.current.press.wasPressedThisFrame;

        public static bool PointerUpThisFrame => Pointer.current.press.wasReleasedThisFrame;

        public static bool PointerPressed => Pointer.current.press.isPressed;

        public static bool PointerOverGameObject => UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        public static float ScrollWheelDelta => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0;

        public static bool RightMouseDownThisFrame => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        public static bool RightMouseUpThisFrame => Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;

        public static int TouchCount
        {
            get
            {
                if (!EnhancedTouchSupport.enabled)
                {
                    return 0;
                }
                else
                {
                    return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count;
                }
            }
        }

        public static void GetPinchInfo(out float radius, out Vector2 centre)
        {
            radius = 0;
            centre = Vector2.zero;

            if (!EnhancedTouchSupport.enabled)
            {
                return;
            }

            var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

            if (activeTouches.Count == 0)
            {
                return;
            }
            else if (activeTouches.Count == 1)
            {
                centre = activeTouches[0].screenPosition;
            }
            else
            {
                foreach (var item in activeTouches)
                {
                    centre += item.screenPosition;
                }

                centre /= activeTouches.Count;
                radius = Vector2.Distance(activeTouches[0].screenPosition, centre);
            }
        }

        public static Vector2 TouchCentre
        {
            get
            {
                if (!EnhancedTouchSupport.enabled || UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0)
                {
                    return PointerPos;
                }
                else
                {
                    var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

                    if (activeTouches.Count == 1)
                    {
                        return activeTouches[0].screenPosition;
                    }
                    else
                    {
                        var centre = Vector2.zero;

                        foreach (var item in activeTouches)
                        {
                            centre += item.screenPosition;
                        }

                        return centre / activeTouches.Count;
                    }
                }
            }
        }
    }

}