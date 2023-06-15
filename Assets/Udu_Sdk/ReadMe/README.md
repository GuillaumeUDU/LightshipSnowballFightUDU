# UDU-SDK

[![Build Status](https://travis-ci.com/example/example-sdk.svg?branch=main)](https://travis-ci.com/example/example-sdk)

Welcome to the UDU Console Software Development Kit (SDK)!

This document provides detailed information on how to get started with the UDU SDK, including system requirements, installation instructions, and usage examples.

## System Requirements

The following system requirements are necessary to use the current version of the SDK:

* Unity: [Unity Version], [Other Required Software]
* Android: [Android Version], [Other Required Software]

Note: The SDK is currently available for Unity and Android, but will be expanded to support IOS soon and additional platforms in the future. Stay tuned for updates on our supported platforms.

## Installation

To install the UDU SDK, follow these steps:

1. Download the `udu_sdk` folder.
2. Go to and open your desired unity project.
3. To Window > Package Manager.
4. Click on the `+` in the top left cornor and select `add package from disk...`.
5. Go to the `udu_sdk` folder and find the `package.json` file and open it.

For more detailed instructions, please refer to the [UDU Console Documentation](https://docs.google.com/document/d/1MhnQzvsfIXCH4WiEq1HZxx_gDPYDKf9k29LIC1J3ItQ/edit?usp=sharing).

## Usage

The UDU SDK provides a set of APIs and tools that enable developers to build applications with the `UDU Console`. Here are some steps to help you get started:

1. Import `the UDU SDK` into your Unity project as decribed in the `Installation` section.
2. Add the `BLEdatastream` prefab to your scene.

Here's an example of how you could use the UDU SDK to control a character in a Unity game:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UduConsole;

public class UDUController : MonoBehaviour
{
    // Console integration
	public AbstractDataStream uduConsoleDatastream;

    void Update(){
        if (!ConsoleIntegration.Instance.isConnected)
        {
            Debug.log("Console not connected")
			return;
        }
        if (uduConsoleDatastream == null)
        {
			uduConsoleDatastream = ConsoleIntegration.Instance.uduConsoleDatastream;
			//uduConsoleDatastream.ConsoleAwake("cube",red,none);           ##Not inplimented
		}
        Move();
    }
    void Move(){
        Vector3 moveDirection characterTransform.forward * uduConsoleDatastream.GetTrackpadDirection().y + characterTransform.right * uduConsoleDatastream.GetTrackpadDirection().x;
        transform.position += moveDirection * Time.deltaTime
    }
}
```

For more detailed usage instructions and API documentation, please refer to the [UDU Console Documentation](https://docs.google.com/document/d/1MhnQzvsfIXCH4WiEq1HZxx_gDPYDKf9k29LIC1J3ItQ/edit?usp=sharing).

## Support

If you have any questions or issues with the SDK Name SDK, please contact us at [SDK@udu.dk](mailto:SDK@udu.dk).

## License

The UDU SDK is released under the [??? License](LICENSE.md).

---
