# Redpoint.Windows.HandleManagement

This library allows you to query all handles currently open on a system and then forcibly close them. You can use it to programmatically unlock files that are currently in-use before you try to delete them.

To query and close native handles, use the static methods inside the `NativeHandles` class.