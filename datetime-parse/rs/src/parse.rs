// Handwritten UTF8 to digit parsing.
//  _ne suffix means no error checking (no looking for non-digit bytes)
//  _#ne suffix means it's implemented for a specific number of digits

pub fn u32(value: &[u8]) -> Option<u32> {
    let mut result: u32 = 0;
    let mut valid = true;

    for c in value {
        let digit = u8::wrapping_sub(*c, b'0');
        
        result *= 10;
        result += digit as u32;

        valid &= digit < 10;
    }

    if valid { Some(result) } else { None }
}

pub fn u32_ne(value: &[u8]) -> u32 {
    let mut result: u32 = 0;

    for c in value {
        let digit = u8::wrapping_sub(*c, b'0');
        
        result *= 10;
        result += digit as u32;
    }

    result
}

pub fn u16(value: &[u8]) -> Option<u16> {
    let mut result: u16 = 0;
    let mut valid = true;

    for c in value {
        let digit = u8::wrapping_sub(*c, b'0');
        
        result *= 10;
        result += digit as u16;

        valid &= digit < 10;
    }

    if valid { Some(result) } else { None }
}

pub fn u16_ne(value: &[u8]) -> u16 {
    let mut result: u16 = 0;

    for c in value {
        let digit = u8::wrapping_sub(*c, b'0');
        
        result *= 10;
        result += digit as u16;
    }

    result
}

pub fn u16_4ne(value: &[u8]) -> u16 {
    1000u16 * u8::wrapping_sub(value[0], b'0') as u16 
     + 100u16 * u8::wrapping_sub(value[1], b'0') as u16
     + 10u16 * u8::wrapping_sub(value[2], b'0') as u16
     + u8::wrapping_sub(value[3], b'0') as u16
}

pub fn u8(value: &[u8]) -> Option<u8> {
    let mut result: u8 = 0;
    let mut valid = true;

    for c in value {
        let digit = u8::wrapping_sub(*c, b'0');
        
        result *= 10;
        result += digit;

        valid &= digit < 10;
    }

    if valid { Some(result) } else { None }
}

// 185 ms
pub fn u8_ne(value: &[u8]) -> u8 {
    let mut result: u8 = 0;

    for c in value {
        let digit = u8::wrapping_sub(*c, b'0');//u8 = *c - b'0';
        
        result *= 10;
        result += digit;
    }

    result
}

// 117 ms
pub fn u8_2ne(value: &[u8]) -> u8 {
    10 * u8::wrapping_sub(value[0], b'0') + u8::wrapping_sub(value[1], b'0')
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse() {
        assert_eq!(Some(1234567890), u32("1234567890".as_bytes()));
        assert_eq!(None, u32("12345678?0".as_bytes()));

        assert_eq!(1234567890, u32_ne("1234567890".as_bytes()));

        assert_eq!(Some(65432u16), u16("65432".as_bytes()));
        assert_eq!(65432u16, u16_ne("65432".as_bytes()));

        assert_eq!(Some(255u8), u8("255".as_bytes()));
        assert_eq!(255u8, u8_ne("255".as_bytes()));
        
        assert_eq!(99u8, u8_2ne("99".as_bytes()));
    }
}

