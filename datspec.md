# Samase ext dat format

**[Binary structures](#binary-structures)**

**[Versions](#versions)**
- [Minor version changes](#minor-version-changes)

**[Field flags](#field-flags)**

**[Fields](#fields)**
- [Single integer](#single-integer)
- [Multiple integers](#multiple-integers)
- [Dat requirement offset, Dat requirement buffer](#dat-requirement-offset-dat-requirement-buffer)
- [Variable length list](#variable-length-list)
    * [List example](#list-example)

**[Field forward compatibility note](#field-forward-compatibility-note)**

**[List of fields and their formats](#list-of-fields-and-their-formats)**
- [Vanilla fields](#vanilla-fields)
- [Extended fields](#extended-fields)

# Binary structures

All integers are in little endian.

```
ExtDatRoot: (At start of file)
0x0     u32 magic ("Dat+", 0x2b746144)
0x4     u16 major_version (1)
0x6     u16 minor_version (3)
0x8     u32 entry_count
0xc     u32 field_count
0x10    Field fields[field_count]
```

```
Field: (0xc bytes)
0x0     u16 field_id
0x2     u16 flags
0x4     u32 file_offset (From start of file)
0x8     u32 length (bytes)
```

# Versions
Major version should be 1. Plan is to increment if there are breaking changes in format (never)

Minor version is currently 3. It gets incremented when the editor should automatically once write
some default values that the user can edit afterwards.

## Minor version changes
- V2 Adds default dummy reqs (Is Landed building) for spread creep (0x66) and place addon (0x24)
    orders.dat, necessary since tatti doesn't make distinction between no reqs enabled and no reqs
    disabled.
- V3 Fixes place addon req from "Is Landed building" to "Has no Addon"
    (Lifted off buildings couldn't build addons)

# Field flags

The only flags that are currently defined are that for fields that have single integer value per
entry, masking `flags & 0x3` is used to define the entry size.
- 0x0 u8
- 0x1 u16
- 0x2 u32
- 0x3 u64 (Not really supported)

# Fields

How to parse the field data differs from field id to another (See below for list).

The formats that have been defined are:

### Single integer
The most common format. Requires checking value of `flags & 3` to determine exact size of each entry

```
SingleIntegerField:
flags & 0x3:
- 0x0 u8 data[entry_count]
- 0x1 u16 data[entry_count]
- 0x2 u32 data[entry_count]
- 0x3 u64 data[entry_count] (Not really supported)
```

Note: The vanilla .dat format has some entries where array size is less than entry count,
such as units.dat infestation being only defined for ids 106 to 228. These kind of subsets
aren't supported; existing undersized fields get expanded to have a value for each entry.

### Multiple integers
E.g. Unit dimension box, where the data is `u16[entry_count * 2]` containing width and height
for each unit. The flags may be set to match the integer width like in `Single integer` format,
but it can be assumed that a specific `Multiple integers` field has always same integer width
that the list below specifies.

### Dat requirement offset, Dat requirement buffer
Dat requirements are represented in format that BW expects them to be: If offset is 0, the
requirement is "Disabled", otherwise it is an index to the buffer of u16-sized requirement opcodes
(NOTE: in u16s, not bytes), which is terminated with 0xffff. The opcode reference is at end
of this file.

This dat format uses one field to store the buffer offset (Using the vanilla field id that BW kind of
does support but is left 0xffff for every unit), and a second field (with a new id) to store the
buffer.

### Variable length list
The requirement buffers with 0xffff-terminated lists are bit awkward to work with, so any
extended dat needing variable length lists will use the following format where two fields
are used to contain offset and length, to one or more buffers.

The offset/length fields use `Single integer` format - check field flags for integer width.
Buffer fields are also `Single integer` format, using `flags & 0x3` to determine width, their
entry count just isn't tied to dat entry count.

If the list has more complex structure than single integer, the same offset and length are
used to index to multiple sibling buffers.

#### List example
Upgrade effects for upgrade ids `#0`, `#1`, `#2`, all affecting unit 55
- `#0` has 3 effects:
    * Movement speed +50% from level 1
    * Movement speed +25% from level 2
    * Attack speed from level 3
 - `#1` has 1 effect:
    * Attack speed from level 1 that is removed at level 3
 - `#3` has 2 effects:
    * Attack speed from level 1
    * Movement speed -50% from level 1, removed at level 2
See list later on for details on how the integers are interpreted

```
Offsets (id 0x14) [0, 3, 4]
Lengths (id 0x15) [3, 1, 2]
Effect Types (Id 0x16) [0, 0, 1, 1, 1, 0]
Effect Min levels (Id 0x17) [1, 2, 3, 1, 1, 1]
Effect Max levels (Id 0x18) [255, 255, 255, 3, 255, 2]
Effect units (Id 0x19) [55, 55, 55, 55, 55, 55]
Effect values (Id 0x1a) [512, 256, 0, 0, 0, -512]
```

## Field forward compatibility note
Dat files written with older versions of a program may not have every extended field that are
listed below. Anybody accessing extended fileds should have a reasonable fallback values in such case.
Vanilla fields should always exist.

## List of fields and their formats

Any field that has no comment describing its format is in `Single integer` format.

### Vanilla fields

These field ids match the array ordering in vanilla .dat files.

Units.dat:
- 0x00 Flingy
- 0x01 Subunit
- 0x02 Subunit 2
- 0x03 Infestation
- 0x04 Construction image
- 0x05 Direction
- 0x06 Has shields
- 0x07 Shields
- 0x08 Hitpoints
- 0x09 Elevation level
- 0x0a Floating
- 0x0b Rank
- 0x0c Ai idle order
- 0x0d Human idle order
- 0x0e Return to idle order
- 0x0f Attack unit order
- 0x10 Attack move order
- 0x11 Ground weapon
- 0x12 Ground weapon hits
- 0x13 Air weapon
- 0x14 Air weapon hits
- 0x15 AI flags
- 0x16 Flags
- 0x17 Target acquisition range
- 0x18 Sight range
- 0x19 Armor upgrade
- 0x1a Armor type
- 0x1b Armor
- 0x1c Rclick action
- 0x1d Ready sound
- 0x1e First what sound
- 0x1f Last what sound
- 0x20 First annoyed sound
- 0x21 Last annoyed sound
- 0x22 First yes sound
- 0x23 Last yes sound
- 0x24 Placement box
    Multiple integers, u16, 2 per entry
- 0x25 Addon position
    Multiple integers, u16, 2 per entry
- 0x26 Dimension box
    Multiple integers, u16, 4 per entry
- 0x27 Portrait
- 0x28 Mineral cost
- 0x29 Gas cost
- 0x2a Build time
- 0x2b Datreq offset
    Dat requirement offset for unit creation (Buffer is in field 0x40)
- 0x2c Group flags
- 0x2d Supply provided
- 0x2e Supply cost
- 0x2f Space required
- 0x30 Space provided
- 0x31 Build score
- 0x32 Kill score
- 0x33 Map label
- 0x34 ???
- 0x35 Misc flags

Weapons.dat:
- 0x00 Label
- 0x01 Flingy
- 0x02 ???
- 0x03 Flags
- 0x04 Min range
- 0x05 Max range
- 0x06 Upgrade
- 0x07 Damage type
- 0x08 Behaviour
- 0x09 Death time
- 0x0a Effect
- 0x0b Inner splash
- 0x0c Middle splash
- 0x0d Outer splash
- 0x0e Damage
- 0x0f Upgrade bonus
- 0x10 Cooldown
- 0x11 Factor
- 0x12 Attack angle
- 0x13 Launch spin
- 0x14 X offset
- 0x15 Y offset
- 0x16 Error msg
- 0x17 Icon

Flingy.dat:
- 0x00 Sprite ID
- 0x01 Top speed
- 0x02 Acceleration
- 0x03 Halt distance
- 0x04 Turn speed
- 0x05 Unused
- 0x06 Movement type

Sprites.dat:
- 0x00 Image
- 0x01 Healthbar
- 0x02 Unknown2
- 0x03 Start as visible
- 0x04 Selection circle
- 0x05 Image pos

Images.dat:
- 0x00 Grp
- 0x01 Can turn
- 0x02 Clickable
- 0x03 Full iscript
- 0x04 Draw if cloaked
- 0x05 Drawfunc
- 0x06 Remapping
- 0x07 Iscript header
- 0x08 Shields Overlay
- 0x09 Attack Overlay
- 0x0a Damage Overlay
- 0x0b Special Overlay
- 0x0c Landing Overlay
- 0x0d Liftoff Overlay

Upgrades.dat:
- 0x00 Mineral cost
- 0x01 Mineral factor
- 0x02 Gas cost
- 0x03 Gas factor
- 0x04 Time cost
- 0x05 Time factor
- 0x06 Dat req offset
    Dat requirement offset for upgrade research (Buffer is in field 0x10)
- 0x07 Icon
- 0x08 Label
- 0x09 Race
- 0x0a Repeat count
- 0x0b Brood war

Techdata.dat:
- 0x00 Mineral cost
- 0x01 Gas cost
- 0x02 Time cost
- 0x03 Energy cost
- 0x04 Dat req research offset
    Dat requirement offset for tech research (Buffer is in field 0x10)
- 0x05 Dat req use offset
    Dat requirement offset for tech use (Buffer is in field 0x11)
- 0x06 Icon
- 0x07 Label
- 0x08 Unk?
- 0x09 Misc?
- 0x0a Brood War

Portdata.dat:
- 0x00 Idle path
- 0x01 Talking path
- 0x02 Idle 0 weight
- 0x03 Idle 1 weight
- 0x04 Idle 2 weight
- 0x05 Idle 3 weight

Orders.dat:
- 0x00 Label
- 0x01 Use weapon targeting
- 0x02 Secondary order (unused)
- 0x03 Non-subunit (unused)
- 0x04 Subunit inherits
- 0x05 Subunit can use (unused)
- 0x06 Interruptable
- 0x07 Stop moving before next queued
- 0x08 Can be queued
- 0x09 Keep target while disabled
- 0x0a Clip to walkable terrain
- 0x0b Fleeable
- 0x0c Requires moving (unused)
- 0x0d Order weapon
- 0x0e Order tech
- 0x0f Animation
- 0x10 Icon
- 0x11 Requirement offset
    Dat requirement offset for order use (Buffer is in field 0x20)
- 0x12 Obscured order

### Extended fields
Units.dat:
- 0x40 Dat requirement buffer
    `Dat requirement buffer`.
- 0x41 Wireframe mode
    `Single integer`. 0 = Segmented (Terran/Protoss), 1 = Gradual (Zerg)
- 0x42 Wireframe ID
    `Single integer`. Frame id for all wireframe GRPs (grpwire, tranwire, wirefram)
- 0x43 Icon ID
    `Single integer`. Frame id for cmdicons GRP
- 0x44 Buttons
    `Single integer`. Entry index in buttons.dat
- 0x45 Linked Buttons Unit
    `Single integer`. Links another unit as compatible with this unit's buttonset?? Not 100% sure
    how this works.
- 0x46 Speed multiplier
    `Single integer`. Automatic speed multiplier that BW had hardcoded for some units.
    In 1024-fixedpoint - 512 is 50% bonus, 256 is 25% etc.
- 0x47 Ext flags
    `Single integer`. 0x1 = Attack speed upgrade, other may be used in future.
- 0x48 Turret max angle
    `Single integer`. Used to prevent goliath turrets from going to far from parent unit's angle.

Ugprades.dat:
- 0x10 Req buffer
    `Dat requirement buffer`.
- 0x12 (offset), 0x13 (length) Attached units
    `Variable length list`. Contains units that transfer this upgrade when given to another player
    * 0x14 Unit list
- 0x14 (offset), 0x15 (length) Extended effects
    `Variable length list`. Various effects that are softcoded.
    * 0x16 Effect type
        - 0 Movement speed buff, value is signed 1024-fixedpoint integer for speed change
        - 1 Attack speed buff, value is unused
    * 0x17 Min level
        Minimum level for which the effect applies
    * 0x18 Max level
        Last level for which the effect applies.
    * 0x19 Unit
        Unit that the upgrade affects.
    * 0x1a Value
        Parameter for the upgrade, depends on Effect type

Techdata.dat:
- 0x10 Research req buffer
    `Dat requirement buffer`.
- 0x11 Use req buffer
    `Dat requirement buffer`.
- 0x12 (offset), 0x13 (length) Attached units
    `Variable length list`. Contains units that transfer this tech when given to another player
    * 0x14 unit list

Buttons.dat:
- 0x00 (offset), 0x01 (length) Buttons
    `Variable length list`. Contains buttons of a buttonset.
    * 0x02 Button position
    * 0x03 Icon
    * 0x04 Disabled string
    * 0x05 Enabled string
    * 0x06 Condition
    * 0x07 Condition param
    * 0x08 Action
    * 0x09 Action param
