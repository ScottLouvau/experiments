After running the excellent [ConvertOneNote2Markdown](https://github.com/theohbrothers/ConvertOneNote2MarkDown) tool, there were still a few issues to fix up in our OneNote notes before importing them into Obsidian.

I used the ConvertOneNote2Markdown config.ps1 settings:
```
# One root media folder for images
`$medialocation = 1` (One root media folder)

# Convert to Markdown style tables; remove raw HTML
- `$conversion = 'markdown-simple_tables-multiline_tables-grid_tables-link_attributes-raw_html+pipe_tables-bracketed_spans+native_spans+startnum`

# Remove header at top of notes
$headerTimestampEnabled = 2 
```

I then ran this code to fix the following issues:
- File Names were "over sanitized", particularly losing spaces.
- Titles were emitted on the same line as the first content.
- All newlines doubled.
- Trailing newlines.
- Image Links should use Obsidian format `![[FileName.png]]`.
- URLs sometimes wrapped in '\<' '\>'
- Some literal characters \('\(, '\[, '\>') needed to be escaped.

I also:
- Used [ImageMagick](https://imagemagick.org/index.php) to scale images down to a maximum of 720 pixels
    - `magick <inFilePath> -strip -auto-orient -resize 720x720> -quality 75 "<outFilePath>"`
- Used [PNGQuant](https://pngquant.org/) to reduce PNG sizes.
    - `pngquant --force ---skip-if-larger "<outFilePath>" -- "<inFilePath>"`
- Manually copied and pasted lost tables from OneNote into Obsidian.
    - The lost tables were HTML in the export and omitted.
