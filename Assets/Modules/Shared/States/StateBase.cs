// Copyright 2022 Niantic, Inc. All Rights Reserved.
﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Class that represents the base of any game state, primarily used to minimize code
    /// duplication and enable easier behavior alterations based on one or more other states'
    /// behaviors.
    ///
    /// NOTE: Currently this is only partially implemented, and should be expanded later.
    /// </summary>
    public abstract class StateBase : MonoBehaviour
    {
        // Used to allow other classes to know if this state's behavior has been skipped.
        public virtual bool Skipped { get; protected set; }
    }
}