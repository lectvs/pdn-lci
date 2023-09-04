# pdn-lci
This is a Paint.NET plugin for creating and editing my custom image type, Lectvs Composite Image (LCI).

# Requirements
- Paint.NET 4.3 or later

# Installation Instructions
- Copy `lci.dll` to `[Paint.NET root directory]/FileTypes`
  - On Windows, this is probably `C:\Program Files\paint.net\FileTypes`
- Open Paint.NET. You should now be able to open and save using the .lci extension

# What is a Lectvs Composite Image?
- An LCI is a layered image (like a .pdn or .psd) but in a JSON format that can be parsed by my game engine.
- LCI layers can contain attributes that affect how that layer behaves in-game.
- When an LCI is loaded into the game, its layers are added to the world as separate WorldObjects defined by the layer's attributes.

# LCI Layers and Attributes
Attributes on a layer are specified in the layer's name, in the following format: `name|attribute1=value1|attribute2=value2|...`

The following attributes are supported:
- restrict (true|false) - if true, the layer exports only the tightest bounding box surrounding all of the opaque pixels in the image. useful for things like props and buttons
- layer (any) - the World layer the object will be put into
- anchor (x,y) - the object's anchor point (e.g. 0,0 means the top-left and 0.5,1 means the bottom-middle)
- offset (x,y) - the render offset of the sprite relative to its position
- physicsGroup (any) - the World physics group the object will be put into
- bounds (x,y,w,h|all) - the physics bounding box of the object. use "all" to specify the tightest bounding box around the opaque pixels
- placeholder (any) - specifies a WorldObject class from the game to put into the world in place of this layer. the value of this attribute should be the name of a constructor that takes the following arguments: (x: number, y: number)
- multibounds (true|false) - if true, this layer will define an optimized set of physics bounding boxes that fit the exact shape of this layer's opaque pixels. useful if you want to define an entire level's collision in a single layer

Any attributes that are not listed above will be added to the object's "data" field as arbitrary key-value pairs.

# The "defaults" layer
- A layer in your image named "defaults" will define default attributes that apply to all layers unless they are overridden in a given layer.
- For example: `defaults|restrict|layer=main`

# Reference: LCI File Schema
- An LCI file starts with ".LCI" and contains a JSON structure immediately after:

### Document
```
{
    width (number) - the width of the image
    height (number) - the height of the image
    layers (Layer[]) - list of layers, ordered from back to front
}
```

### Layer
```
{
    rawName (string) - the raw, full name of the layer as seen in Paint.NET
    name (string) - the name of the layer without attached attributes
    image (string) - a data URI containing a base64 png representation of the pixels in the layer
    position (x, y) - the center point of the layer relative to the top-left corner
    isDataLayer (bool) - true iff this layer is not intended to be rendered
    properties (LayerProperties) - attributes of the layer
    visible (bool) - whether the layer is visible
    opacity (number) - the opacity (transparency) of the layer from 0-255
    blendMode (number) - the blend mode of the layer
    offsetX (number) - used internally
    offsetY (number) - used internally
}
```

### LayerProperties
```
{
    restrict (bool) - if true, the png data is restricted to the tightest bounding box around the layer's opaque contents
    layer (string) - the World layer that should be used when this layer is turned into a WorldObject
    anchor (x, y) - the anchor that should be used
    offset (x, y) - the render offset that should be used
    physicsGroup (string) - the World physics group that should be used
    bounds (Rect) - the physics bounds given to the WorldObject
    placeholder (string) - if set, will replace this layer with a WorldObject of the named class
    multiBounds (Rect[]) - if set, this layer defines many bounding boxes that will be used in the world's physics
    data (Object) - dictionary of arbitrary key-value pairs that will be attached to the WorldObject
}
```