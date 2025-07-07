using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Contract;

public class MerkleTree
{
    public byte[][] leaves { get; private set; }
    public List<List<byte[]>> levels { get; private set; }

    public MerkleTree(byte[][] leaves, List<List<byte[]>> levels)
    {
        this.leaves = leaves;
        this.levels = levels;
    }

    
    public MerkleProof GenerateProof(uint leafIndex)
    {
        //Debug.Log("=== generate_proof START ===");
        //Debug.Log($"Leaf index: {leafIndex}");
        //Debug.Log($"Tree levels: {levels.Count}");

        List<byte[]> siblings = new List<byte[]>();
        uint currentIndex = leafIndex;

        for (int level = 0; level < levels.Count - 1; level++)
        {
            List<byte[]> levelNodes = levels[level];
            uint siblingIndex = (currentIndex % 2 == 0) ? currentIndex + 1 : currentIndex - 1;

            //Debug.Log($"Level {level}: current_index={currentIndex}, sibling_index={siblingIndex}, level_nodes_count={levelNodes.Count}");

            byte[] siblingHash = levelNodes[(int)siblingIndex];
            //Debug.Log($"  Adding sibling: {BitConverter.ToString(siblingHash)}");
            siblings.Add(siblingHash);

            currentIndex = currentIndex / 2;
            //Debug.Log($"  Next level index: {currentIndex}");
        }

        //Debug.Log($"Generated proof with {siblings.Count} siblings");
        for (int i = 0; i < siblings.Count; i++)
        {
            //Debug.Log($"  Sibling {i}: {BitConverter.ToString(siblings[i])}");
        }

        MerkleProof proof = new MerkleProof
        {
            leaf_index = leafIndex,
            siblings = siblings.ToArray(),
        };

        //Debug.Log("=== generate_proof END ===");
        return proof;
    }

    public byte[] Root()
    {
        List<byte[]> rootLevel = levels[levels.Count - 1];
        return rootLevel[0];
    }

    public static (byte[], MerkleTree) BuildMerkleTree(byte[][] leaves)
    {
        //Debug.Log("=== build_merkle_tree START ===");
        //Debug.Log($"Number of leaves: {leaves.Length}");

        for (int i = 0; i < leaves.Length; i++)
        {
            //Debug.Log($"Leaf {i}: {BitConverter.ToString(leaves[i])}");
        }

        if (leaves.Length == 0)
        {
            byte[] zeroRoot = new byte[16]; // All zeros
            List<List<byte[]>> emptyLevels = new List<List<byte[]>>
            {
                new List<byte[]> { zeroRoot }
            };
            MerkleTree emptyTree = new MerkleTree(new byte[0][], emptyLevels);
            //Debug.Log($"Empty tree, returning zero root: {BitConverter.ToString(zeroRoot)}");
            return (zeroRoot, emptyTree);
        }

        List<byte[]> paddedLeaves = new List<byte[]>(leaves);
        byte[] emptyHash = new byte[16]; // All zeros

        // Find next power of 2
        uint targetSize = 1;
        while (targetSize < leaves.Length)
        {
            targetSize *= 2;
        }

        // Pad with empty hashes
        while (paddedLeaves.Count < targetSize)
        {
            paddedLeaves.Add((byte[])emptyHash.Clone());
        }

        //Debug.Log($"Padded from {leaves.Length} to {paddedLeaves.Count} leaves");

        List<List<byte[]>> levels = new List<List<byte[]>>();
        levels.Add(new List<byte[]>(paddedLeaves));
        //Debug.Log($"Level 0 (padded leaves): {paddedLeaves.Count} nodes");

        int currentLevel = 0;
        while (levels[currentLevel].Count > 1)
        {
            List<byte[]> currentNodes = levels[currentLevel];
            List<byte[]> nextLevel = new List<byte[]>();

            //Debug.Log($"Processing level {currentLevel}, nodes: {currentNodes.Count}");

            for (int i = 0; i < currentNodes.Count; i += 2)
            {
                byte[] left = currentNodes[i];
                byte[] right = currentNodes[i + 1];

                //Debug.Log($"  Pair {i / 2}: left={BitConverter.ToString(left)}, right={BitConverter.ToString(right)}");

                // Combine left and right (32 bytes total)
                byte[] combinedBytes = new byte[32];
                Array.Copy(left, 0, combinedBytes, 0, 16);
                Array.Copy(right, 0, combinedBytes, 16, 16);

                //Debug.Log($"  Combined bytes: {BitConverter.ToString(combinedBytes)}");

                // Hash the combined bytes and take first 16 bytes
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] fullHash = sha256.ComputeHash(combinedBytes);
                    byte[] parentHash = new byte[16];
                    Array.Copy(fullHash, 0, parentHash, 0, 16);

                    //Debug.Log($"  Parent hash: {BitConverter.ToString(parentHash)}");
                    nextLevel.Add(parentHash);
                }
            }

            levels.Add(nextLevel);
            currentLevel++;
            //Debug.Log($"Level {currentLevel} complete, {nextLevel.Count} nodes");
        }

        byte[] root = levels[levels.Count - 1][0];

        //Debug.Log($"Final root: {BitConverter.ToString(root)}");
        //Debug.Log($"Total levels: {levels.Count}");

        MerkleTree tree = new MerkleTree(leaves, levels);

        //Debug.Log("=== build_merkle_tree END ===");
        return (root, tree);
    }

    public static bool VerifyMerkleProof(byte[] leaf, MerkleProof proof, byte[] root)
    {
        //Debug.Log("=== verify_merkle_proof START ===");
        //Debug.Log($"Leaf hash: {BitConverter.ToString(leaf)}");
        //Debug.Log($"Proof leaf_index: {proof.leaf_index}");
        //Debug.Log($"Proof siblings count: {proof.siblings.Length}");
        //Debug.Log($"Expected root: {BitConverter.ToString(root)}");

        byte[] currentHash = (byte[])leaf.Clone();
        uint index = proof.leaf_index;

        //Debug.Log($"Starting with current_hash: {BitConverter.ToString(currentHash)}, index: {index}");

        for (int level = 0; level < proof.siblings.Length; level++)
        {
            byte[] sibling = proof.siblings[level];
            //Debug.Log($"--- Level {level} ---");
            //Debug.Log($"Current hash: {BitConverter.ToString(currentHash)}");
            //Debug.Log($"Sibling hash: {BitConverter.ToString(sibling)}");
            //Debug.Log($"Current index: {index}");

            // Create a 32-byte array for concatenation
            byte[] combinedBytes = new byte[32];

            // Determine order based on index (even = current is left, odd = current is right)
            if (index % 2 == 0)
            {
                // Current hash goes on the left, sibling on the right
                //Debug.Log($"Index {index} is even: current (left) + sibling (right)");
                Array.Copy(currentHash, 0, combinedBytes, 0, 16);
                Array.Copy(sibling, 0, combinedBytes, 16, 16);
            }
            else
            {
                // Sibling goes on the left, current hash on the right
                //Debug.Log($"Index {index} is odd: sibling (left) + current (right)");
                Array.Copy(sibling, 0, combinedBytes, 0, 16);
                Array.Copy(currentHash, 0, combinedBytes, 16, 16);
            }

            //Debug.Log($"Combined bytes: {BitConverter.ToString(combinedBytes)}");

            // Hash the combined bytes and take first 16 bytes as the new current hash
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] fullHash = sha256.ComputeHash(combinedBytes);
                currentHash = new byte[16];
                Array.Copy(fullHash, 0, currentHash, 0, 16);
            }

            //Debug.Log($"New current hash: {BitConverter.ToString(currentHash)}");

            // Move up the tree
            index = index / 2;
            //Debug.Log($"New index: {index}");
        }

        //Debug.Log($"Final computed hash: {BitConverter.ToString(currentHash)}");
        //Debug.Log($"Expected root hash: {BitConverter.ToString(root)}");

        bool result = ArraysEqual(currentHash, root);
        //Debug.Log($"=== verify_merkle_proof END: {result} ===");

        return result;
    }

    private static bool ArraysEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}