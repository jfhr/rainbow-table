# rainbow-table

Experimental MD5 rainbow table implementation. 

**Disclaimer: This project is for academic purposes only and not recommended for real-world use.**

## Install

### Build from source

- Install the .NET 5 x64 SDK from [get.dot.net](https://get.dot.net)
- Clone the repository and cd into it
- Run `dotnet build -c Release ./Rainbow.csproj`

### Use prebuilt binaries

- Install the .NET 5 runtime from [get.dot.net](https://get.dot.net)
- Download the latest Rainbow.dll from the releases tab

## Use

- Run `dotnet Rainbow.dll`
- Enter an example password: This will determine whether the table will contain passwords with upper-/lowercase letters and/or digits, and the length of the passwords
- Enter the hash size in bytes. If it is less than 16, we will take only the first n bytes of the MD5 hash
- Enter length of a row in the rainbow table (number of hashes per row)
- Enter the thread count (1 foreground thread and n threads for building)

The table will start building. Press `q` at anytime to pause.

When paused, you can enter a hash value in hexadecimal to search for it. Submit an empty value to go back to building.

### Console colors

Set the environment variable `CLICOLOR` to `0` or `NO_COLOR` to any value to disable colored output.
