CFLAGS=-I. -D_FILE_OFFSET_BITS=64 `pkg-config fuse --cflags --libs`

bbfs: bbfs.c log.c
	gcc -o bbfs bbfs.c log.c $(CFLAGS)
