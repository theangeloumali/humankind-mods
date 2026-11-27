"""Read-only: prints a bundle's UnityFS header fields, to compare an original with a rebuilt copy."""
import struct
import sys

for path in sys.argv[1:]:
    data = open(path, "rb").read(200)
    sig_end = data.index(b"\0")
    pos = sig_end + 1
    version = struct.unpack(">I", data[pos:pos + 4])[0]
    pos += 4
    player_end = data.index(b"\0", pos)
    player = data[pos:player_end].decode()
    pos = player_end + 1
    engine_end = data.index(b"\0", pos)
    engine = data[pos:engine_end].decode()
    pos = engine_end + 1
    size, compressed, uncompressed, flags = struct.unpack(">qIII", data[pos:pos + 20])
    print(f"{path.split('/')[-1][:50]}: v{version} {player} {engine} size={size} "
          f"blockinfo={compressed}/{uncompressed} flags=0x{flags:x} "
          f"(compression={flags & 0x3f}, combined={bool(flags & 0x40)}, at_end={bool(flags & 0x80)}, pad={bool(flags & 0x200)})")
