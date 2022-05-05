# HTML Builder

This is a small utility built primarily to reduce the tedium of editing my portfolio website.

# Feature Set
- Configurable output location
- Insertion of html files into templates
- Multi-level dependency support (eg. index.html <- Projects.html <- Projects/)
- Injection and extraction point mapping via name and attribute queries
- Multiple output support

# Future Considerations
- Folder as consumer support (perform insertion on each file in folder)
- Retrieval and deployment of referenced media
- Site deployment through FTP
- Data parsing (Potentially using a configurable ruleset?)
- Improved documentation
- General code health improvements

# Usage
There are two primary objects HTML Builder uses:
- **Reference ('ref')** - Defines a file or folder location
- **Mapping ('map')** - Defines an extraction->insertion operation between two References

## Reference
You must first define your references. The syntax for doing so is as follows:<br>
`ref set [key] [file/folder] [relative path] [options]`<br>
- Key: identifies the reference and for 'head' references defines the output file name
- File/Folder: defines whether the reference is a file or folder
- Relative path: The path from the executable location to the file/folder.

### Options
- `--reverse` - instructs the builder to read the files from a reference folder in reverse order.

## Mapping
After all references are defined, you should define mappings. The syntax is as follows:<br>
`map set [key] [consumer] [contributor] --consumer [search values] --contributor [search values] [options]`<br>
- Key: identifier, like References
- Consumer: The reference that will be *inserted into* during this operation
- Contributor: The reference that will be *extracted from* during this operation
### Search Values
The arguments prefixed by `--` are mandatory but can be in any order and apply to the earlier arguments of the same name.<br>
The search values that immediately follow them are in the form `attribute=value` and represent HTML/XML attributes to match when searching for the insertion and extraction points.<br>
These values should result in a singular unique result within their associated reference for the build process to succeed. Any values can be used except for `name` which is a reserved keyword that searches for the element name (eg. `name=test` matches `<test />`).

### Options
- `--unpack` - instructs the builder to treat the matched contributor extraction element as a container, inserting its *children* into the consumer.

## Commands
Input `help` to display a list of available commands and their descriptions.

### ref
The `ref` command is split into 3 subcommands: `set`, `remove`, and `list`.
#### set
Creates or modifies a reference.<br>
`ref set [key] [file/folder] [relative path] [options]`
#### remove
Removes an existing reference.<br>
`ref remove [key]`
#### list
Lists all references.<br>
`ref list [-p, -m]`<br>
- -p --path: displays the path
- -m --mapping: displays references' mappings

### map
`map` is also split into the same 3 subcommands.
#### set
Creates or modifies a mapping.<br>
`map set [key] [consumer] [contributor] --consumer [search values] --contributor [search values] [options]`<br>
#### remove
Removes an existing mapping.<br>
`map remove [key]`<br>
#### list
Displays all mappings.<br>
`map list [-v]`<br>
- -v --verbose: displays further information about the mapping