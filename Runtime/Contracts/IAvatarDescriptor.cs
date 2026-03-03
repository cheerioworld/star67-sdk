using System.Collections.Generic;

namespace Star67
{
  public interface IAvatarDescriptor
  {
    AvatarType Type { get; }
    string AvatarId { get; }
    string Uri { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
  }
}