use std::{fs, path::{Path, PathBuf}};
use regex::Captures;

mod sanitize_filename;

fn main() {
    let args = std::env::args().collect::<Vec<String>>();

    if args.len() != 3 {
        println!("Usage: {} <root_path> <output_path>", args.get(0).unwrap());
        return;
    }

    let input_root_path = Path::new(args.get(1).unwrap());
    let output_root_path = Path::new(args.get(2).unwrap());

    // Find all files under the root path, and get the path part under the root path
    let mut files = Vec::new();
    add_all_files_under(input_root_path, &mut files);

    for file in files {
        adjust_file(&file, &input_root_path, &output_root_path);
    }
}

fn add_all_files_under(root_path: &Path, files: &mut Vec<PathBuf>) {
    for entry in std::fs::read_dir(root_path).unwrap() {
        let entry = entry.unwrap();
        let path = entry.path();
        if path.is_file() {
            files.push(path);
        } else {
            add_all_files_under(&path, files);
        }
    }
}

fn is_markdown_file(path: &Path) -> bool {
    if let Some(extension) = path.extension() {
        let extension = extension.to_string_lossy();
        let extension = extension.to_ascii_lowercase();
        return extension == "md";
    }

    false
}

fn adjust_file(input_path: &Path, input_root_path: &Path, output_root_path: &Path) {
    // Only adjust Markdown files
    if !is_markdown_file(input_path) {
        return;
    }

    // Read the file
    let content = fs::read_to_string(&input_path).unwrap();

    // Don't adjust files with lost HTML tables
    // if content.contains("[TABLE]") {
    //     println!(" [TABLE]: {}", input_path.to_string_lossy());
    //     return;
    // }

    // Rebuild output filename from title in document
    let file_name = input_path.file_stem().unwrap().to_string_lossy();
    let title = &content[2..(2+file_name.len())];
    let new_file_name = sanitize_filename::sanitize_filename(title);
    let path_under_root = input_path.strip_prefix(input_root_path).unwrap();
    let output_under_root = path_under_root.with_file_name(new_file_name).with_extension("md");
    let output_under_root = output_under_root.as_path();
    let output_path = output_root_path.join(output_under_root);

    // Keeping original file names to allow merging with prior work
    // let path_under_root = input_path.strip_prefix(input_root_path).unwrap();
    // let output_path = output_root_path.join(path_under_root);

    println!("{}", output_path.strip_prefix(output_root_path).unwrap().to_string_lossy());

    // Update the contents
    let content = adjust_markdown(content, input_path);

    // Write the converted markdown
    fs::create_dir_all(output_path.parent().unwrap()).unwrap();
    fs::write(output_path, content).unwrap();
}

fn adjust_markdown(mut content: String, input_path: &Path) -> String {
    let file_name = input_path.file_stem().unwrap().to_string_lossy();

    // Note starts with '# FileName'
    if content.starts_with(&"# ") {
        let content_bytes = content.as_bytes();
        let file_name_bytes = file_name.as_bytes();
        if let Some(last) = content_bytes.get(file_name_bytes.len() + 1) {
            if *last == *file_name.as_bytes().last().unwrap() {
                content = format!("{}\n{}", &content[0..file_name.len()+2], &content[file_name.len()+2..]);
            }
        }
    }

    // All newlines are doubled
    content = content.replace("\n\n", "\n");

    // Remove extra trailing newlines
    content = content.trim_end().to_string();

    // Replace image links with the Obsidian reference style
    let img_regex = regex::Regex::new(r"!\[[^\n\]]*]\((\.\./)+media/([^\n/\)]+)\)").unwrap();
    content = img_regex.replace_all(&content, |caps: &Captures| format!("![[{}]]", &caps[2])).to_string();

    // Remove OneNote URL wrapping '< >'
    let url_regex = regex::Regex::new(r"<(http[^>\n]+)>").unwrap();
    content = url_regex.replace_all(&content, |caps: &Captures| format!("{}", &caps[1])).to_string();

    // Escape non-URL left parens
    content = content.replace("(", "\\(");

    // Escape non-image left brackets
    let bracket_regex = regex::Regex::new(r"([^!\[\]\)])\[").unwrap();
    content = bracket_regex.replace_all(&content, |caps: &Captures| format!("{}\\[", &caps[1])).to_string();

    // Escape left angle
    content = content.replace("<", "\\<");

    content
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_add_all_files_under() {
        let root_path = Path::new("./src");

        let mut files = Vec::new();
        add_all_files_under(root_path, &mut files);

        let me = files.iter().find(|f| f == &&PathBuf::from("./src/main.rs"));
        assert!(me.is_some());
    }

    #[test]
    fn adjust_markdown_test() {
        let input_path = PathBuf::from("./tst/2022-Vacations.md");
        let content = fs::read_to_string(&input_path).unwrap();

        // Convert the content, and write a copy for manual examination
        let content = adjust_markdown(content, &input_path);
        fs::write("./tst/2022-Vacations-Out.md", &content).unwrap();

        // Verify newlines de-doubled
        // (There should be two newlines after Market? and only one after Beaches:)
        assert!(content.contains("Market?\n\nNon-Swimmable Beaches:\nPueblo"));

        // Verify trailing newlines removed
        assert!(content.ends_with("phone."));

        // Verify title removed (next thing is an image link)
        assert!(content.starts_with("# 2022 Vacations\n!["));

        // Verify image link turned into Obsidian reference style
        assert!(content.contains("![[zARCHIVE-_Archive-2022-2022-Vacations-image1.png]]"));

        // Verify URL in '<' '>' unwrapped
        assert!(content.contains("https://docs.espressif.com/"));

        // Verify left parens escaped
        assert!(content.contains("\\(Rose)"));

        // Verify left brackets escaped
        assert!(content.contains("\\[+$20pp]"));
    }
}