"""Build the native bootstrap's chunked Windows-Compression-API payload (stdlib only)."""
import ctypes
import hashlib
from pathlib import Path
import struct
import sys

source, stub, destination = map(Path, sys.argv[1:4])
files = sorted(path for path in source.rglob('*') if path.is_file())
cabinet = ctypes.WinDLL('cabinet', use_last_error=True)
cabinet.CreateCompressor.argtypes = [ctypes.c_uint32, ctypes.c_void_p, ctypes.POINTER(ctypes.c_void_p)]
cabinet.Compress.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
cabinet.CloseCompressor.argtypes = [ctypes.c_void_p]
handle = ctypes.c_void_p()
if not cabinet.CreateCompressor(4, None, ctypes.byref(handle)):
    raise ctypes.WinError(ctypes.get_last_error())
digest = hashlib.sha256()
try:
    with destination.open('wb') as out:
        out.write(stub.read_bytes())
        offset = out.tell()
        def write(data):
            digest.update(data)
            out.write(data)
        write(struct.pack('<8sIQ', b'KUBPKG1\0', len(files), sum(path.stat().st_size for path in files)))
        for path in files:
            name = str(path.relative_to(source)).replace('/', '\\').encode('utf-8')
            size = path.stat().st_size
            with path.open('rb') as inp:
                file_hash = hashlib.file_digest(inp, 'sha256').digest()
            write(struct.pack('<IQ32sI', len(name), size, file_hash, (size + 1048575) // 1048576))
            write(name)
            with path.open('rb') as inp:
                while raw := inp.read(1048576):
                    needed = ctypes.c_size_t()
                    cabinet.Compress(handle, raw, len(raw), None, 0, ctypes.byref(needed))
                    if not needed.value:
                        raise ctypes.WinError(ctypes.get_last_error())
                    compressed = ctypes.create_string_buffer(needed.value)
                    if not cabinet.Compress(handle, raw, len(raw), compressed, needed.value, ctypes.byref(needed)):
                        raise ctypes.WinError(ctypes.get_last_error())
                    packed = compressed.raw[:needed.value]
                    if len(packed) >= len(raw):
                        write(struct.pack('<II', len(raw), 0)); write(raw)
                    else:
                        write(struct.pack('<II', len(raw), len(packed))); write(packed)
        length = out.tell() - offset
        out.write(struct.pack('<8sQQ32s', b'KUBEND1\0', offset, length, digest.digest()))
finally:
    cabinet.CloseCompressor(handle)
print(f'Packed {len(files)} files into {destination.name}: {destination.stat().st_size:,} bytes')
