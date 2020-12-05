extern crate libc;

// Creates a new instance of Blake3 Hasher
#[no_mangle]
pub extern fn blake3_new() -> *mut blake3::Hasher {
  return Box::into_raw(Box::new(blake3::Hasher::new()));
}

// Deletes an existing a new instance of Blake3 Hasher
#[no_mangle]
pub extern fn blake3_delete(hasher: *mut blake3::Hasher) {
  unsafe{ Box::from_raw(hasher) };
}

// Updates Blake3 hash with data
#[no_mangle]
pub extern fn blake3_update(
  hasher: *mut blake3::Hasher,
  ptr: *const u8,
  size: libc::size_t)
{
  let hasher = unsafe { &mut *hasher };
  let slice = unsafe { std::slice::from_raw_parts(ptr as *const u8, size as usize) };
  hasher.update(slice);  
}

// Finalize the hash and put the result into output.
#[no_mangle]
pub extern fn blake3_finalize(
  hasher: *mut blake3::Hasher,
  ptr: *mut u8,
  size: libc::size_t)
{
  let hasher = unsafe { &mut *hasher };
  let slice = unsafe { std::slice::from_raw_parts_mut(ptr as *mut u8, size as usize) };
  hasher.finalize_xof().fill(slice);
}
