# How to downgrade spine 3.7's resources to 3.6, 3.5 or 3.4

First of all, it should be noted that using SkeletonViewer and the JsonRollback command is only applicable for downgrading from version 3.8 to 3.7. If you use the JsonRollback command in the SkeletonViewer of version 3.7, the Json data will be rolled back to a version of data that is not known (based on a quick look, it is estimated to be around version 2.1).

The key issue is that if you use a feature that is only available in a certain version, you will not be able to open the data in an older version. The AttachmentType information is a good indicator of these new features. Here is an explanation using the C# SDK version 3.7:

In version 3.7 of the C# SDK, AttachmentType is recorded in:

        spine-csharp/src/Attachments/AttachmentType.cs 

This cs file contains the following:

    public enum AttachmentType {
		Region, Boundingbox, Mesh, Linkedmesh, Path, Point, Clipping
	}

This information corresponds to the "type" field in the .json resource output by Spine.

If the new version of resources uses a feature that is only available in the current version, the old version will not be able to read it.

For example, the AttachmentType in version 3.6 is the same as in version 3.7, so the output resources can be shared. Here is the AttachmentType content in version 3.6:

https://github.com/EsotericSoftware/spine-runtimes/blob/3.6.53/spine-csharp/src/Attachments/AttachmentType.cs

    public enum AttachmentType {
		Region, Boundingbox, Mesh, Linkedmesh, Path, Point, Clipping
	}

The AttachmentType content in version 3.5 is as follows:

https://github.com/EsotericSoftware/spine-runtimes/blob/3.5.51/spine-csharp/src/Attachments/AttachmentType.cs

    public enum AttachmentType {
		Region, Boundingbox, Mesh, Linkedmesh, Path
	}


As you can see, Point and Clipping are missing in version 3.5. This means that if you use these two features in Spine 3.7 (or 3.6), version 3.5 will not be able to read this resource.

The solution is simple:

1. Use the Spine 3.7 version editor to open the Spine file, remove the new features (Point, Clipping) that are used, and then export the file again. Theoretically, the resource without the new features can be read by version 3.5 (whether it is the editor or runtime SDK).

2. Edit the output .json file directly. Delete the nodes that use Point or Clipping. The key words are probably "type": "point" or "type": "clipping".

Of course, after removing the new features, Spine may need some adjustments, which should be evaluated on a case-by-case basis.

# Very Important Info about Spine Version 3.4
This is a problematic version, and it is best not to use it. The official version has been abandoned, and this version is not discussed.

Although Path is available in versions 3.5, 3.6, and 3.7, there are issues with Path when opening version 3.4.

Please do not use version 3.4.

<br>

# Additional Note
The above information is based on my own testing, so there may be errors. However, I have successfully downgraded the resources from Spine 3.7 to version 3.4 and was able to use them. Discussion and feedback are welcome.

<br>

---
This document is translated by ChatGPT.
