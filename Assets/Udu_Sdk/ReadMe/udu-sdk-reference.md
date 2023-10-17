# UDU SDK Reference Documentation

## Data Getters

### GetTimestamp

`GetTimestamp() -> long`

##### Description

A brief overview of what the function does and how it should be used.

##### Parameters

none

##### Return Value

Returns the timestamp as a long decriping how long the console has been turned on

##### Example Usage

```csharp
    private long priorTimeStamp;
    private long timeRestiction = 10f;

    void Start()
    {
       priorTimeStamp = GetTimestamp();
    }
    void Update()
    {
       if ((priorTimeStamp + timeRestiction) < GetTimestamp() ){
            //do something
       }
    }

```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.

### GetAcceleration

`GetAcceleration() -> Vector3`

##### Description

GetAcceleration get the IMU acceleration as a Vector3.

##### Parameters

##### Return Value

Returns Vector3 of the acceleration data with gravity removed.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
GetAcceleration();
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.

### GetAngularVelocity

`GetAngularVelocity() -> Vector3`

##### Description

GetAngularVelocity get the IMU angular velocity as a Vector3.

##### Parameters

##### Return Value

Vector3 form the gyroscope.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.

### GetOrientation

`functionName(param1: type, param2: type) -> returnType`

##### Description

A brief overview of what the function does and how it should be used.

##### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

##### Return Value

A description of what the function returns and under what conditions.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.

### GetTrackpadCoordinates

`functionName(param1: type, param2: type) -> returnType`

##### Description

A brief overview of what the function does and how it should be used.

##### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

##### Return Value

A description of what the function returns and under what conditions.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.


### GetTrackpadDirection

`functionName(param1: type, param2: type) -> returnType`

##### Description

A brief overview of what the function does and how it should be used.

##### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

##### Return Value

A description of what the function returns and under what conditions.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.


### GetMagneticHeading

`functionName(param1: type, param2: type) -> returnType`

##### Description

A brief overview of what the function does and how it should be used.

##### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

##### Return Value

A description of what the function returns and under what conditions.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.


### GetEdgeImpulseData

`functionName(param1: type, param2: type) -> returnType`

##### Description

A brief overview of what the function does and how it should be used.

##### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

##### Return Value

A description of what the function returns and under what conditions.

##### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

##### Notes

Any additional notes or caveats about the function that may be relevant to users.

## Output Setters

### SetVibrationAndStart

`functionName(param1: type, param2: type) -> returnType`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage
```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```
#### Notes

Any additional notes or caveats about the function that may be relevant to users.

### SetDisplayFile

`functionName(param1: type, param2: type) -> returnType`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage
```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```
#### Notes

Any additional notes or caveats about the function that may be relevant to users.

### SetLED

`functionName(param1: type, param2: type) -> returnType`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage
```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```
#### Notes

Any additional notes or caveats about the function that may be relevant to users.

### SetLEDOff

`functionName(param1: type, param2: type) -> returnType`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage
```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```
#### Notes

Any additional notes or caveats about the function that may be relevant to users.

### SetLEDFlashingColor

`functionName(param1: type, param2: type) -> returnType`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage
```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```
#### Notes

Any additional notes or caveats about the function that may be relevant to users.

### SetImageAndLEDs

`functionName(param1: type, param2: type) -> returnType`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `param1`: A description of what the parameter represents and any constraints or limitations.
* `param2`: A description of what the parameter represents and any constraints or limitations.

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage
```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```
#### Notes

Any additional notes or caveats about the function that may be relevant to users.

### SetLEDConstantColor

`functionName(Color color, int brightness) ->`

#### Description

A brief overview of what the function does and how it should be used.

#### Parameters

* `color`: .
* `brightness`: .

#### Return Value

A description of what the function returns and under what conditions.

#### Example Usage

```csharp
// A brief example of how the function can be used in code
// Demonstrates the function call and expected output or behavior
functionName(param1, param2);
```

#### Notes

Any additional notes or caveats about the function that may be relevant to users.