import struct

path = 'C:/pelei/scbw_yms/v116/starcraft.exe'
out_path = 'Resources/buttons.bin'
BASE = 0x0040_0000
BUTTON_SETS = 0x005187E8

# Buttons.bin format
# u16 buttonset_count
# u16 button_count
# u8 unit_id_to_buttonset [0xfa]
# u16 unit_id_linked [0xfa]
# u16 buttonset_first_button[buttonset_count]
# u8 buttonset_len[buttonset_count]
# u8 button_pos[button_count]
# u16 icon[button_count]
# u16 disabled_string[button_count]
# u16 enabled_string[button_count]
# u16 cond[button_count]
# u16 cond_param[button_count]
# u16 act[button_count]
# u16 act_param[button_count]

def main():
    exe = open(path, 'rb').read()
    sections = parse_section_map(exe)
    sets = []
    buttons_root = [(0, 0)]
    buttons = []
    va_map = (exe, sections)
    for i in range(0xfa):
        count = va_read_u32(va_map, BUTTON_SETS + i * 0xc)
        ptr = va_read_u32(va_map, BUTTON_SETS + i * 0xc + 4)
        linked = va_read_u32(va_map, BUTTON_SETS + i * 0xc + 8)
        sets.append((ptr, count, linked))
    # (ptr, count) -> buttons index
    used_sets = {}
    unit_result = []
    for (ptr, count, linked) in sets:
        if count == 0:
            unit_result.append((0, linked))
        elif (ptr, count) in used_sets:
            unit_result.append((used_sets[(ptr, count)], linked))
        else:
            index = len(buttons_root)
            used_sets[(ptr, count)] = index
            buttons_root.append((len(buttons), count))
            unit_result.append((index, linked))
            for i in range(count):
                arr = [
                    va_read_u16(va_map, ptr + i * 0x14),
                    va_read_u16(va_map, ptr + i * 0x14 + 2),
                    va_read_u16(va_map, ptr + i * 0x14 + 0x12),
                    va_read_u16(va_map, ptr + i * 0x14 + 0x10),
                    va_read_u32(va_map, ptr + i * 0x14 + 0x4),
                    va_read_u16(va_map, ptr + i * 0x14 + 0xc),
                    va_read_u32(va_map, ptr + i * 0x14 + 0x8),
                    va_read_u16(va_map, ptr + i * 0x14 + 0xe),
                ]
                buttons.append(arr)

    cond_fnptr_remap = {}
    act_fnptr_remap = {}

    for (i, (unit_id, index)) in enumerate(COND_REMAPS):
        button_index = buttons_root[unit_result[unit_id][0]][0] + index
        ptr = buttons[button_index][4]
        assert not ptr in cond_fnptr_remap
        cond_fnptr_remap[ptr] = i

    for (i, (unit_id, index)) in enumerate(ACT_REMAPS):
        button_index = buttons_root[unit_result[unit_id][0]][0] + index
        ptr = buttons[button_index][6]
        assert not ptr in act_fnptr_remap
        act_fnptr_remap[ptr] = i

    for arr in buttons:
        arr[4] = cond_fnptr_remap[arr[4]]
        arr[6] = act_fnptr_remap[arr[6]]

    buf = bytearray()
    buf += struct.pack('<HH', len(buttons_root), len(buttons))
    #for (i, (button_id, linked)) in enumerate(unit_result):
        #print(f'{i:x}: {button_id:x} - {linked:x}')
    #for (i, arr) in enumerate(buttons):
        #print(f'{i:x}: {arr[4]:x} - {arr[5]:x}')

    for (button_id, linked) in unit_result:
        buf.append(button_id)
    for (button_id, linked) in unit_result:
        buf += struct.pack('<H', linked)
    for (start, length) in buttons_root:
        buf += struct.pack('<H', start)
    for (start, length) in buttons_root:
        buf.append(length)
    for i in range(8):
        for arr in buttons:
            if i == 0:
                buf.append(arr[i])
            else:
                buf += struct.pack('<H', arr[i])

    out = open(out_path, 'wb')
    out.write(buf)


def va_read_u32(va_map, address):
    for (virt, phys, size) in va_map[1]:
        if virt <= address and virt + size > address:
            offset = address - virt
            return read_u32(va_map[0], phys + offset)
    raise Exception(f'Can\'t read {address:x}')

def va_read_u16(va_map, address):
    # Acceptable
    return va_read_u32(va_map, address) & 0xffff

# array of (virtual, physical, size)
def parse_section_map(exe):
    result = []

    header = read_u32(exe, 0x3c)
    section_count = read_u16(exe, header + 6)
    opt_head_size = read_u16(exe, header + 8 + 0xc)
    section_pos = opt_head_size + header + 8 + 0x10

    for i in range(0, section_count):
        sect_raw = exe[(section_pos + 40 * i):][:40]
        sect = struct.unpack('=8sIIIIIIHHI', sect_raw)
        result.append((BASE + sect[2], sect[4], sect[3]))
    return result

def read_u32(bytes, offset):
    return struct.unpack('<I', bytes[offset:][:4])[0]

def read_u16(bytes, offset):
    return struct.unpack('<H', bytes[offset:][:2])[0]

COND_REMAPS = [
    (248, 1),
    (248, 2),
    (248, 0),
    (229, 0),
    (35, 0),
    (72, 5),
    (244, 0),
    (244, 1),
    (244, 2),
    (124, 0),
    (160, 5),
    (116, 10),
    (236, 0),
    (234, 0),
    (236, 1),
    (236, 2),
    (136, 4),
    (136, 3),
    (64, 3),
    (64, 4),
    (11, 5),
    (11, 6),
    (136, 0),
    (46, 5),
    (2, 5),
    (136, 2),
    (1, 5),
    (1, 6),
    (246, 5),
    (246, 6),
    (5, 5),
    (5, 6),
    (5, 0),
    (1, 8),
    (172, 0),
    (113, 0),
    (113, 7),
    (113, 8),
    (37, 5),
    (37, 6),
    (38, 5),
    (38, 0),
    (38, 2),
    (41, 3),
    (41, 4),
    (103, 1),
    (134, 0),
    (41, 5),
    (41, 6),
    (72, 2),
    (81, 2),
    (67, 6),
    (67, 7),
    (61, 5),
    (61, 6),
    (64, 5),
    (64, 6),
    (7, 8),
    (7, 0),
    (7, 1),
    (7, 2),
    (7, 3),
    (7, 4),
    (7, 5),
    (7, 6),
    (7, 7),
    (108, 0)
];

ACT_REMAPS = [
    (235, 0),
    (234, 0),
    (236, 0),
    (111, 1),
    (160, 5),
    (5, 6),
    (5, 5),
    (231, 0),
    (36, 0),
    (41, 0),
    (41, 1),
    (41, 2),
    (50, 2),
    (124, 1),
    (72, 0),
    (72, 1),
    (81, 1),
    (72, 2),
    (81, 2),
    (72, 5),
    (72, 3),
    (72, 4),
    (136, 0),
    (136, 3),
    (46, 5),
    (0, 5),
    (136, 2),
    (136, 4),
    (116, 10),
    (239, 0),
    (197, 0),
    (240, 0),
    (113, 6),
    (238, 0),
    (134, 0),
    (131, 3),
    (113, 7),
    (7, 3),
    (38, 5),
    (7, 4),
    (7, 5),
    (37, 5),
    (37, 6),
    (1, 5),
    (1, 6),
    (113, 8),
    (11, 5),
    (11, 6),
    (67, 7),
    (61, 6),
    (172, 0),
    (1, 8),
    (237, 0),
    (34, 4),
    (248, 1),
    (248, 0),
    (248, 2),
    (229, 0),
    (230, 0),
    (7, 6),
]

main()
