### 4. WiX for COM Registration (The Best Practice)

The best and most reliable way to register a COM component with WiX is to **avoid running `regasm.exe`** and instead let WiX manage the registry keys declaratively. The `heat.exe` tool from the WiX Toolset is designed for this.

**Step 1: Generate the WiX Fragment with `heat.exe`**
1.  Open a command prompt (the "Developer Command Prompt for VS" is best).
2.  Navigate to your `MyCompany.Logging` project's output directory (e.g., `bin\Debug`).
3.  Run the `heat.exe` command. `heat.exe` is located in your WiX Toolset's `bin` folder.

    ```cmd
    "C:\Program Files (x86)\WiX Toolset v3.11\bin\heat.exe" file MyCompany.Logging.dll -dr INSTALLFOLDER -cg ComBridgeComponents -var var.SourceDir -out ComBridgeRegistry.wxs
    ```
    *   `file ...dll`: The DLL to inspect.
    *   `-dr INSTALLFOLDER`: The directory ID where this DLL will be installed.
    *   `-cg ComBridgeComponents`: The name of the `ComponentGroup` to create.
    *   `-var var.SourceDir`: A variable to use for the source path, making it portable.
    *   `-out ...wxs`: The name of the output file to generate.

**Step 2: Review the Generated File (`ComBridgeRegistry.wxs`)**
`heat` will produce a file containing all the necessary `<Component>`, `<File>`, and `<RegistryValue>` elements. It will look complex, full of GUIDs—this is correct. It's everything `regasm` would have done, but now it's declarative and managed by MSI.

**Example `ComBridgeRegistry.wxs` (abbreviated):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
    <Fragment>
        <DirectoryRef Id="INSTALLFOLDER">
            <Component Id="MyCompany.Logging.dll" Guid="PUT-GUID-HERE">
                <File Id="MyCompany.Logging.dll" KeyPath="yes" Source="$(var.SourceDir)\MyCompany.Logging.dll" />
                <RegistryValue Root="HKCR" Key="CLSID\{F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE}" ... />
                <RegistryValue Root="HKCR" Key="CLSID\{F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE}\InprocServer32" ... />
                <!-- Many more registry keys... -->
            </Component>
        </DirectoryRef>
    </Fragment>
    <Fragment>
        <ComponentGroup Id="ComBridgeComponents">
            <ComponentRef Id="MyCompany.Logging.dll" />
        </ComponentGroup>
    </Fragment>
</Wix>
```

**Step 3: Integrate into Your Main WiX Project**
1.  Add the generated `ComBridgeRegistry.wxs` file to your WiX project.
2.  In your main `Product.wxs` file, define the `SourceDir` variable and reference the component group.

    ```xml
    <Wix ...>
      <Product ...>
        <!-- Define the variable that heat used -->
        <?define SourceDir="Path\To\Your\ComBridge\bin\Debug" ?>

        <Feature Id="ProductFeature" Title="My Application" Level="1">
          <!-- Add a reference to the generated component group -->
          <ComponentGroupRef Id="ComBridgeComponents" />
          <!-- ... other components and features ... -->
        </Feature>
      </Product>
    </Wix>
    ```

This approach is superior to a Custom Action running `regasm` because the installation is **transactional**. If the install fails or the user uninstalls the product, MSI guarantees that all these registry keys will be cleanly and correctly removed.