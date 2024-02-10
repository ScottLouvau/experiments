
pub fn sanitize_filename(value: &str) -> String {
    let mut result = String::new();

    for c in value.chars() {
        match c {
            // Separators: Allowed separator, but only once
            '\\'|'/'|'|'|':'|';' => {
                if !result.ends_with('-') {
                    result.push('-');
                }
            },

            // Punctuation: Nothing
            '?'|'!'|'.'|',' => { },

            // Wrappers: Nothing
            '('|')'|'['|']'|'<'|'>' => { },

            // "And" shorthand -> "and"
            '&'|'+' => result.push_str("and"),

            _ => result.push(c),
        }
    }

    result
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn file_name_escape() {
        // Spaces not sanitized
        assert_eq!(sanitize_filename("2022 Vacations"), "2022 Vacations");

        // Wrapping characters and punctuation removed
        assert_eq!(sanitize_filename("(2022)?!?"), "2022");

        // Separators
        assert_eq!(sanitize_filename("Archive/2022\\Vacations|International:Europe"), "Archive-2022-Vacations-International-Europe");

        // Combination, "and" markers
        assert_eq!(sanitize_filename("Nice <([Gift])> Ideas + History & Log"), "Nice Gift Ideas and History and Log");
    }
}