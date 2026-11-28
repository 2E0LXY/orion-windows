package uk.community.orion.sync;
import java.io.IOException;
import java.util.List;

/** Minimal file store over the user's Drive appDataFolder (or an in-memory fake in tests). */
public interface DriveStore {
 final class FileInfo {
  public final String id,name;public final long created;
  public FileInfo(String id,String name,long created){this.id=id;this.name=name;this.created=created;}
 }
 List<FileInfo> list() throws IOException;
 byte[] read(String id) throws IOException;
 /** Creates the file when existingId is null, otherwise replaces its content. Returns the file id. */
 String write(String name,byte[] data,String existingId) throws IOException;
 void delete(String id) throws IOException;
}
