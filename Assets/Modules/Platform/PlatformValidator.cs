// Copyright 2022 Niantic, Inc. All Rights Reserved.
namespace Niantic.ARVoyage
{

    /// <summary>
    /// Reports a compiler error when attempting to target an invalid platform for AR Voyage.
    /// </summary>

#if !(UNITY_IOS || UNITY_ANDROID)
    #error "Only the iOS and Android Platforms are supported.  Please switch your target platform in the build settings."
#endif

}